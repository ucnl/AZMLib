// AZMLib/AuxAZMPort.cs
using UCNLDrivers;
using UCNLDrivers.uAux;
using UCNLNMEA;

namespace AZMLib
{
    public class AuxAZMPort : uAuxPort
    {
        #region Properties

        private static bool _nmeaSingleton = false;

        private bool _isWaitingLocal;
        public bool IsWaitingLocal
        {
            get => _isWaitingLocal;
            private set
            {
                if (_isWaitingLocal != value)
                {
                    _isWaitingLocal = value;
                    IsWaitingLocalChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private ICs? _lastQueryID = ICs.IC_INVALID;

        public bool IsDeviceInfoValid { get; private set; }
        public AZM_DEVICE_TYPE_Enum DeviceType { get; private set; }
        public REMOTE_ADDR_Enum RemoteAddress { get; private set; }
        public ushort AddressMask { get; private set; }
        public AZM_PTS_TYPE_Enum PTSType { get; private set; }
        public string SerialNumber { get; private set; } = string.Empty;
        public string SystemInfo { get; private set; } = string.Empty;
        public string SystemVersion { get; private set; } = string.Empty;
        public int ChannelID { get; private set; }

        private const int DefaultTimeoutMs = 1000;
        private const int RSTSTimeoutMs = 3000;

        #endregion

        #region IAuxSource

        public override string Id { get; }
        public override AuxSourceKind Kind => AuxSourceKind.Custom;

        #endregion

        #region Constructor

        public AuxAZMPort(string id, BaudRate baudRate)
            : base(baudRate)
        {
            Id = id;
            PortDescription = "AZM";
            IsLogIncoming = true;
            IsTryAlways = true;

            NMEAInit();
        }

        #endregion

        #region NMEA Init

        private static void NMEAInit()
        {
            if (_nmeaSingleton) return;
            _nmeaSingleton = true;

            NMEAParser.AddManufacturerToProprietarySentencesBase(ManufacturerCodes.AZM);

            var sentences = new[]
            {
                ("0", "x,x"),                   // IC_D2H_ACK       '0'  $PAZM0,[cmdID],result
                ("1", "x,x.x,x.x,x.x"),         // IC_D2D_STRSTP    '1'  $PAZM1,[addrMask],[sty_PSU],[soundSpeed_mps],[maxDist_m]
                ("2", "x,x.x"),                 // IC_D2D_RSTS      '2'  $PAZM2,[addr],[sty_PSU]
                ("3", "x,x,x,x,x.x,x.x,x.x,x.x,x.x,x.x,x.x,x.x,x.x,x.x,x.x,x.x"), //IC_D2H_NDTA  '3' $PAZM3,status,[addr],[rq_code],[rs_code],[msr_dB],[p_time],[s_range],[p_range],[r_dpt],[a],[e],[lprs],[ltmp],[lhdn],[lpts],[lrol]
                ("4", "x.x"),                   // IC_H2D_DPTOVR    '4'  $PAZM4,depth_m
                ("5", "x"),                     // IC_D2H_RUCMD     '5'  $PAZM5,cmdID
                ("6", "x"),                     // IC_D2H_RBCAST    '6'  $PAZM6,cmdID
                ("7", "x,x"),                   // IC_H2D_CREQ      '7'  $PAZM7,[addr],user_data_id
                ("8", "x,x,x"),                 // IC_H2D_CSET      '8'  $PAZM8,user_data_id,user_data_value,[reserved]
                ("?", "x"),                     // IC_H2D_DINFO_GET '?'  $PAZM?,[reserved]
                ("!", "x,x,c--c,c--c,x,x,x,x"), // IC_D2H_DINFO     '!' $PAZM!,d_type,address,serialNumber,sys_info,sys_version,pts_type,dl_ch_id,ul_ch_id
            };            

            foreach (var (id, format) in sentences)
                NMEAParser.AddProprietarySentenceDescription(ManufacturerCodes.AZM, id, format);
        }

        #endregion

        #region Send helpers

        private bool TrySend(string message, ICs queryID)
        {
            if (!detected || IsWaitingLocal)
                return false;

            try
            {
                Send(message);

                int timeout = queryID == ICs.IC_D2D_RSTS ? RSTSTimeoutMs : DefaultTimeoutMs;
                StartTimer(timeout);

                IsWaitingLocal = true;
                _lastQueryID = queryID;

                return true;
            }
            catch (Exception ex)
            {
                LogEventHandler?.Invoke(this, new LogEventArgs(LogLineType.ERROR, ex));
                return false;
            }
        }

        private void SafeParse(Action parseAction, string sentenceType)
        {
            try
            {
                parseAction();
            }
            catch (Exception ex)
            {
                LogError($"Error parsing {sentenceType}", ex);
            }
        }

        #endregion

        #region Queries

        public bool Query_DINFO()
        {
            StopTimer();
            var msg = NMEAParser.BuildProprietarySentence(ManufacturerCodes.AZM, "?", new object[] { 0 });
            return TrySend(msg, ICs.IC_H2D_DINFO_GET);
        }

        public bool Query_BaseStop()
        {
            return Query_STRSTP(0, double.NaN, double.NaN, double.NaN);
        }

        public bool Query_BaseStart(ushort addrMask, double sty_PSU, double maxDist_m)
        {
            return Query_STRSTP(addrMask, sty_PSU, double.NaN, maxDist_m);
        }

        public bool Query_BaseStart(ushort addrMask, double maxDist_m)
        {
            return Query_STRSTP(addrMask, double.NaN, double.NaN, maxDist_m);
        }

        public bool Query_BaseStart(ushort addrMask, double sty_PSU, double soundSpeed_mps, double maxDist_m)
        {
            return Query_STRSTP(addrMask, sty_PSU, soundSpeed_mps, maxDist_m);
        }

        public bool Query_STRSTP(ushort addrMask = 0, double sty_PSU = double.NaN,
            double soundSpeed_mps = double.NaN, double maxDist_m = double.NaN)
        {
            var msg = NMEAParser.BuildProprietarySentence(ManufacturerCodes.AZM, "1",
                new object?[]
                {
                    addrMask > 0 ? (int?)addrMask : null,
                    !double.IsNaN(sty_PSU) ? (double?)sty_PSU : null,
                    !double.IsNaN(soundSpeed_mps) ? (double?)soundSpeed_mps : null,
                    !double.IsNaN(maxDist_m) ? (double?)maxDist_m : null
                });

            return TrySend(msg, ICs.IC_D2D_STRSTP);
        }

        public bool Query_RemoteStySet(double sty_PSU)
        {
            return Query_RSTS(0, sty_PSU);
        }

        public bool Query_RemoteAddrSet(REMOTE_ADDR_Enum addr)
        {
            return Query_RSTS(addr, double.NaN);
        }

        public bool Query_RSTS(REMOTE_ADDR_Enum addr, double sty_PSU)
        {
            var msg = NMEAParser.BuildProprietarySentence(ManufacturerCodes.AZM, "2",
                new object[]
                {
                    addr == REMOTE_ADDR_Enum.REM_ADDR_INVALID ? null : (int)addr,
                    !double.IsNaN(sty_PSU) ? sty_PSU : null
                });

            return TrySend(msg, ICs.IC_D2D_RSTS);
        }

        public bool Query_CREQ(REMOTE_ADDR_Enum addr, CDS_REQ_CODES_Enum rcode)
        {
            var msg = NMEAParser.BuildProprietarySentence(ManufacturerCodes.AZM, "7",
                new object[]
                {
                    addr == REMOTE_ADDR_Enum.REM_ADDR_INVALID ? null : (int)addr,
                    (int)rcode
                });

            return TrySend(msg, ICs.IC_H2D_CREQ);
        }

        public bool Query_CSET_Write(CDS_REQ_CODES_Enum did, int value)
        {
            var msg = NMEAParser.BuildProprietarySentence(ManufacturerCodes.AZM, "8",
                new object[] { (int)did, value, null });

            return TrySend(msg, ICs.IC_D2D_CSET);
        }

        public bool Query_CSET_Read(CDS_REQ_CODES_Enum did)
        {
            var msg = NMEAParser.BuildProprietarySentence(ManufacturerCodes.AZM, "8",
                new object[] { (int)did, null, null });

            return TrySend(msg, ICs.IC_D2D_CSET);
        }

        #endregion

        #region Parsers

        private void Parse_ACK(object[] parameters) => SafeParse(() =>
        {
            ICs sntID = AZM.ICsByMessageID(parameters[0].ToString());
            IC_RESULT_Enum resID = AZM.O2_IC_RESULT_Enum(parameters[1]);

            StopTimer();
            IsWaitingLocal = false;

            ACKReceived?.Invoke(this, new ACKReceivedEventArgs(sntID, resID));
        }, "AZM0");

        private void Parse_RSTS(object[] parameters) => SafeParse(() =>
        {
            StopTimer();
            IsWaitingLocal = false;

            REMOTE_ADDR_Enum addr = AZM.O2_REMOTE_ADDR_Enum(parameters[0]);
            double sty_PSU = AZM.O2D(parameters[1]);

            RSTSReceived?.Invoke(this, new RSTSReceivedEventArgs(addr, sty_PSU));
        }, "AZM2");

        private void Parse_STRSTP(object[] parameters) => SafeParse(() =>
        {
            StopTimer();
            IsWaitingLocal = false;

            ushort addrMask = AZM.O2U16(parameters[0]);
            double sty_PSU = AZM.O2D(parameters[1]);
            double soundSpeed_mps = AZM.O2D(parameters[2]);
            double maxDist_m = AZM.O2D(parameters[3]);

            STRSTPReceived?.Invoke(this, new STRSTPReceivedEventArgs(addrMask, sty_PSU, soundSpeed_mps, maxDist_m));
        }, "AZM1");

        private void Parse_NDTA(object[] parameters) => SafeParse(() =>
        {
            StopTimer();
            if (Status != AuxStatus.Inactive)
                StartTimer(3000);

            NDTA_Status_Enum status = AZM.O2_NDTA_Status_Enum(parameters[0]);
            REMOTE_ADDR_Enum addr = AZM.O2_REMOTE_ADDR_Enum(parameters[1]);
            CDS_REQ_CODES_Enum req_code = AZM.O2_CDS_REQ_CODES_Enum(parameters[2]);
            int res_code = AZM.O2S32(parameters[3]);
            double msr_dB = AZM.O2D(parameters[4]);
            double p_time_s = AZM.O2D(parameters[5]);
            double s_range_m = AZM.O2D(parameters[6]);
            double p_range_m = AZM.O2D(parameters[7]);
            double r_dpt_m = AZM.O2D(parameters[8]);
            double a_deg = AZM.O2D(parameters[9]);
            double e_deg = AZM.O2D(parameters[10]);
            double lprs_mBar = AZM.O2D(parameters[11]);
            double ltmp_C = AZM.O2D(parameters[12]);
            double lhdn_deg = AZM.O2D(parameters[13]);
            double lptc_deg = AZM.O2D(parameters[14]);
            double lrol_deg = AZM.O2D(parameters[15]);

            NDTAReceived?.Invoke(this,
                new NDTAReceivedEventArgs(status, addr, req_code, res_code,
                msr_dB, p_time_s, s_range_m, p_range_m, r_dpt_m,
                a_deg, e_deg, lprs_mBar, ltmp_C, lhdn_deg, lptc_deg, lrol_deg));
        }, "AZM3");

        private void Parse_RUCMD(object[] parameters) => SafeParse(() =>
        {
            CDS_REQ_CODES_Enum cmdID = AZM.O2_CDS_REQ_CODES_Enum(parameters[0]);
            RUCMDReceived?.Invoke(this, new RUCMDReceivedEventArgs(cmdID));
        }, "AZM5");

        private void Parse_RBCAST(object[] parameters) => SafeParse(() =>
        {
            CDS_RBCAST_CODES_Enum cmdID = AZM.O2_CDS_RBCAST_CODES_Enum(parameters[0]);
            RBCASTReceived?.Invoke(this, new RBCASTReceivedEventArgs(cmdID));
        }, "AZM6");

        private void Parse_DINFO(object[] parameters) => SafeParse(() =>
        {
            DeviceType = AZM.O2_AZM_DEVICE_TYPE_Enum(parameters[0]);
            AddressMask = 0;
            RemoteAddress = REMOTE_ADDR_Enum.REM_ADDR_INVALID;

            if (DeviceType == AZM_DEVICE_TYPE_Enum.DT_USBL_TSV)
                AddressMask = AZM.O2U16(parameters[1]);
            else if (DeviceType == AZM_DEVICE_TYPE_Enum.DT_REMOTE)
                RemoteAddress = AZM.O2_REMOTE_ADDR_Enum(parameters[1]);

            SerialNumber = AZM.O2S(parameters[2]);
            SystemInfo = AZM.O2S(parameters[3]);
            SystemVersion = AZM.BCDVersionToStr(AZM.O2S32(parameters[4]));
            PTSType = AZM.O2_AZM_PTS_TYPE_Enum(parameters[5]);
            ChannelID = AZM.O2S32(parameters[6]);

            IsDeviceInfoValid = (DeviceType != AZM_DEVICE_TYPE_Enum.DT_INVALID) &&
                                !string.IsNullOrEmpty(SerialNumber);

            DeviceInfoValidChanged?.Invoke(this, EventArgs.Empty);
        }, "AZM!");

        private void Parse_CSET(object[] parameters) => SafeParse(() =>
        {
            StopTimer();
            IsWaitingLocal = false;

            CDS_REQ_CODES_Enum rcode = AZM.O2_CDS_REQ_CODES_Enum(parameters[0]);
            int value = AZM.O2S32(parameters[1]);

            CSETReceived?.Invoke(this, new CSETReceivedEventArgs(rcode, value));
        }, "AZM8");

        #endregion

        #region uAuxPort overrides

        public override void InitQuerySend()
        {
            var msg = NMEAParser.BuildProprietarySentence(ManufacturerCodes.AZM, "?", new object[] { 0 });
            Send(msg);
        }

        public override void OnClosed()
        {
            StopTimer();
            IsDeviceInfoValid = false;
            IsWaitingLocal = false;
        }

        public override void ProcessIncoming(NMEASentence sentence)
        {
            if (sentence is not NMEAProprietarySentence pSentence ||
                pSentence.Manufacturer != ManufacturerCodes.AZM)
                return;

            if (!detected && "0123568!".Contains(pSentence.SentenceIDString))
            {
                detected = true;
                StopTimer();
            }

            switch (pSentence.SentenceIDString)
            {
                case "0": Parse_ACK(pSentence.parameters); break;
                case "1": Parse_STRSTP(pSentence.parameters); break;
                case "2": Parse_RSTS(pSentence.parameters); break;
                case "3": Parse_NDTA(pSentence.parameters); break;
                case "5": Parse_RUCMD(pSentence.parameters); break;
                case "6": Parse_RBCAST(pSentence.parameters); break;
                case "8": Parse_CSET(pSentence.parameters); break;
                case "!": Parse_DINFO(pSentence.parameters); break;
            }
        }

        #endregion

        #region Helpers

        private void LogError(string context, Exception ex)
        {
            LogEventHandler?.Invoke(this,
                new LogEventArgs(LogLineType.ERROR,
                    $"AuxAZMPort ({PortName}): {context} - {ex.Message}"));
        }

        #endregion

        #region Events

        public EventHandler? IsWaitingLocalChanged;
        public EventHandler? DeviceInfoValidChanged;

        public EventHandler<ACKReceivedEventArgs>? ACKReceived;
        public EventHandler<RSTSReceivedEventArgs>? RSTSReceived;
        public EventHandler<STRSTPReceivedEventArgs>? STRSTPReceived;
        public EventHandler<NDTAReceivedEventArgs>? NDTAReceived;
        public EventHandler<RUCMDReceivedEventArgs>? RUCMDReceived;
        public EventHandler<RBCASTReceivedEventArgs>? RBCASTReceived;
        public EventHandler<CSETReceivedEventArgs>? CSETReceived;

        #endregion
    }
}