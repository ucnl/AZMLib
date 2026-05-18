using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AZMLib
{
    public class CREQResultEventArgs : EventArgs
    {
        public REMOTE_ADDR_Enum RemoteAddress { get; private set; }
        public CDS_REQ_CODES_Enum ReqCode { get; private set; }
        public int ResCode { get; private set; }
        public CREQResultEventArgs(REMOTE_ADDR_Enum addr, CDS_REQ_CODES_Enum req_code, int res_code)
        {
            RemoteAddress = addr;
            ReqCode = req_code;
            ResCode = res_code;
        }
    }

    public class USBLRawDataEventArgs : EventArgs
    {
        public REMOTE_ADDR_Enum RemoteAddress { get; }
        public DateTime Timestamp { get; }

        // Углы
        public double HAngle_deg { get; }
        public double VAngle_deg { get; }

        // Время распространения и дальности
        public double PTime_s { get; }
        public double SlantRange_m { get; }
        public double SlantRangeProjection_m { get; }

        // MSR
        public double MSR_dB { get; }

        // Глубина антенны
        public double StationDepth_m { get; }

        public USBLRawDataEventArgs(
            REMOTE_ADDR_Enum remoteAddress,
            DateTime timestamp,
            double hAngle_deg,
            double vAngle_deg,
            double pTime_s,
            double slantRange_m,
            double slantRangeProjection_m,
            double msr_dB,
            double stationDepth_m)
        {
            RemoteAddress = remoteAddress;
            Timestamp = timestamp;
            HAngle_deg = hAngle_deg;
            VAngle_deg = vAngle_deg;
            PTime_s = pTime_s;
            SlantRange_m = slantRange_m;
            SlantRangeProjection_m = slantRangeProjection_m;
            MSR_dB = msr_dB;
            StationDepth_m = stationDepth_m;
        }
    }

    public class ACKReceivedEventArgs : EventArgs
    {
        // $PAZM0,[cmdID],result

        public ICs SentenceID { get; private set; }
        public IC_RESULT_Enum ResultID { get; private set; }

        public ACKReceivedEventArgs(ICs sntID, IC_RESULT_Enum resID)
        {
            SentenceID = sntID;
            ResultID = resID;
        }
    }

    public class STRSTPReceivedEventArgs : EventArgs
    {
        // $PAZM1,[addrMask],[sty_PSU],[soundSpeed_mps],[maxDist_m]
        public ushort AddrMask { get; private set; }
        public double Sty_PSU { get; private set; }
        public double SoundSpeed_mps { get; private set; }
        public double MaxDist_m { get; private set; }

        public STRSTPReceivedEventArgs(ushort addrMask, double sty_PSU, double soundSpeed_mps, double maxDist_m)
        {
            AddrMask = addrMask;
            Sty_PSU = sty_PSU;
            SoundSpeed_mps = soundSpeed_mps;
            MaxDist_m = maxDist_m;
        }
    }

    public class RSTSReceivedEventArgs : EventArgs
    {
        // $PAZM2,[addr],[sty_PSU]
        public REMOTE_ADDR_Enum Addr { get; private set; }
        public double Sty_PSU { get; private set; }

        public bool IsStyPresent { get => !double.IsNaN(Sty_PSU); }

        public RSTSReceivedEventArgs(REMOTE_ADDR_Enum addr, double sty_PSU)
        {
            Addr = addr;
            Sty_PSU = sty_PSU;
        }
    }

    public class NDTAReceivedEventArgs : EventArgs
    {
        // $PAZM3,status,[addr],[rq_code],[rs_code],[msr_dB],[p_time],[s_range],[p_range],[r_dpt],[a],[e],[lprs],[ltmp],[lhdn],[lpts],[lrol]

        public NDTA_Status_Enum Status { get; private set; }

        public REMOTE_ADDR_Enum Address { get; private set; }

        public CDS_REQ_CODES_Enum RequestCode { get; private set; }

        public int ResponseCode { get; private set; }

        public double MSR_dB { get; private set; }

        public double PropTime_s { get; private set; }

        public double SlantRange_m { get; private set; }

        public double SlantRangeProjection_m { get; private set; }

        public double RemotesDepth_m { get; private set; }

        public double HAngle_deg { get; private set; }

        public double VAngle_deg { get; private set; }

        public double LocPrs_mBar { get; private set; }

        public double LocTemp_C { get; private set; }

        public double LocHeading_deg { get; private set; }

        public double LocPitch_deg { get; private set; }

        public double LocRoll_deg { get; private set; }

        public NDTAReceivedEventArgs(NDTA_Status_Enum status, REMOTE_ADDR_Enum addr, CDS_REQ_CODES_Enum reqCode,
            int resCode, double msr_dB, double p_time, double s_range, double p_range, double r_dpt,
            double a, double e, double lprs, double ltmp, double lhdn, double lpts, double lrol)
        {
            Status = status;
            Address = addr;
            RequestCode = reqCode;
            ResponseCode = resCode;
            MSR_dB = msr_dB;
            PropTime_s = p_time;
            SlantRange_m = s_range;
            SlantRangeProjection_m = p_range;
            RemotesDepth_m = r_dpt;
            HAngle_deg = a;
            VAngle_deg = e;
            LocPrs_mBar = lprs;
            LocTemp_C = ltmp;
            LocHeading_deg = lhdn;
            LocPitch_deg = lpts;
            LocRoll_deg = lrol;
        }
    }

    public class RUCMDReceivedEventArgs : EventArgs
    {
        // $PAZM5,cmdID
        public CDS_REQ_CODES_Enum CmdID { get; private set; }

        public RUCMDReceivedEventArgs(CDS_REQ_CODES_Enum cmdID)
        {
            CmdID = cmdID;
        }
    }

    public class RBCASTReceivedEventArgs : EventArgs
    {
        // $PAZM6,cmdID
        public CDS_RBCAST_CODES_Enum CmdID { get; private set; }

        public RBCASTReceivedEventArgs(CDS_RBCAST_CODES_Enum cmdID)
        {
            CmdID = cmdID;
        }
    }

    public class CSETReceivedEventArgs : EventArgs
    {
        // $PAZM8,dataID,dataVal,reserved
        public CDS_REQ_CODES_Enum UserDataID { get; private set; }
        public int UserDataValue { get; private set; }

        public CSETReceivedEventArgs(CDS_REQ_CODES_Enum udid, int udval)
        {
            UserDataID = udid;
            UserDataValue = udval;
        }
    }
}
