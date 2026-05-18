// AZMLib/AZMManager.cs
using AZMLib.Output;
using System.Collections.Concurrent;
using System.Net;
using UCNLDrivers;
using UCNLDrivers.uAux;
using UCNLNav;
using UCNLNav.TrackFilters;
using UCNLPhysics;

namespace AZMLib
{
    public class AZMManager : IDisposable
    {
        private bool _disposed = false;

        #region External data

        private readonly AuxDataProvider _auxData;

        #endregion

        #region AZM Port

        private readonly AuxAZMPort _azmPort;

        public AuxStatus AZMStatus => _azmPort.Status;
        public bool AZMDetected => _azmPort.Status == AuxStatus.Detected;

        #endregion

        #region Device info

        public AZM_DEVICE_TYPE_Enum DeviceType { get; private set; } = AZM_DEVICE_TYPE_Enum.DT_INVALID;
        public string DeviceSerialNumber { get; private set; } = string.Empty;
        public string DeviceVersionInfo { get; private set; } = string.Empty;

        #endregion

        #region State & Remotes

        public AZMTranscieverState State { get; }
        public ConcurrentDictionary<REMOTE_ADDR_Enum, ResponderBeacon> Remotes { get; }

        #endregion

        #region Settings

        public ushort AddressMask { get; private set; }
        public double MaxDist_m { get; private set; }
        public double Salinity_PSU { get; private set; }
        public double SpeedOfSound_mps { get; private set; }
        public bool IsPSIMSSBOutputEnabled { get; set; }
        public bool USBLRawDataEventEnabled { get; set; }
        public bool IsStationNMEAOutputEnabled { get; set; }
        public bool IsRecalculateRange { get; set; } = true;
        public bool IsPTimeAdjustment { get; set; } = true;

        public double AntennaXOffset_m => _x_offset_m;
        public double AntennaYOffset_m => _y_offset_m;
        public double AntennaPhi_deg => _phi_deg;

        #endregion

        #region Interrogation

        private bool _interrogationActive;

        public bool InterrogationActive
        {
            get => _interrogationActive;
            private set
            {
                if (_interrogationActive != value)
                {
                    _interrogationActive = value;
                    InterrogationActiveChangedHandler?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        #endregion

        #region Filters & Calibration

        private double _phi_deg, _x_offset_m, _y_offset_m;
        private int _usblDhFifoSize = 8;
        private double _usblDhMaxSpeed = 1, _usblDhThreshold = 5;
        private int _usblSFifoSize = 4;
        private double _usblSThreshold = 100;
        private double _dataLatencyMs = 400;
        private readonly CompassHistory _compassHistory = new();
        private readonly WPManager _wpManager = new();
        private readonly AZMAntennaCorrector _antCorrector = new();
        private readonly AngularCalibrationManager _acManager = new();

        public bool AngularCalibration { get; private set; }
        public int AngularCalibrationCollected => _acManager.Count;
        public int AngularCalibrationTotal => _acNPoints;


        private double _acStAngle = 340, _acNdAngle = 20, _acStep = 0.5;
        private int _acNPoints = 512;
        private REMOTE_ADDR_Enum _acBeaconAddr = REMOTE_ADDR_Enum.REM_ADDR_1;

        #endregion

        #region LBL

        private readonly LBLProcessor _lblProcessor = new();
        private DHTrackFilterCartesian? _lblDHFilter;
        private TrackMovingAverageSmootherCartesian? _lblSFilter;
        private double _lblRErrThreshold = 10;
        private double _pTimePerAddrAdjustment = 0.0006;
        private double _pTimePerAddrAdjustment2 = 0.0004;

        #endregion

        #region Output

        private OutputManager _output;

        #endregion

        #region Constructor

        public AZMManager(AuxAZMPort azmPort, AZMSettings settings,
            OutputSettings outputSettings, AuxDataProvider auxData)
        {
            _azmPort = azmPort ?? throw new ArgumentNullException(nameof(azmPort));
            _auxData = auxData ?? throw new ArgumentNullException(nameof(auxData));
            _auxData.OnDataUpdated += OnAuxDataUpdated;

            ValidateSettings(settings);
            InitializeFilters(settings);

            State = new AZMTranscieverState();
            Remotes = new ConcurrentDictionary<REMOTE_ADDR_Enum, ResponderBeacon>();

            SubscribeToAZMEvents();
            InitializeLBL(settings);
            InitializeOutputs(outputSettings);
            _auxData.SubscribeAll();
        }

        #endregion

        #region Initialization helpers

        private void ValidateSettings(AZMSettings settings)
        {
            Salinity_PSU = AZM.IsStyPSU(settings.Salinity_PSU)
                ? settings.Salinity_PSU
                : throw new ArgumentOutOfRangeException(nameof(settings.Salinity_PSU));

            _phi_deg = AZM.IsHdnDeg(settings.AntennaAngularOffset_deg)
                ? settings.AntennaAngularOffset_deg
                : throw new ArgumentOutOfRangeException(nameof(settings.AntennaAngularOffset_deg));

            MaxDist_m = AZM.IsMaxDst(settings.MaxDist_m)
                ? settings.MaxDist_m
                : throw new ArgumentOutOfRangeException(nameof(settings.MaxDist_m));

            SpeedOfSound_mps = AZM.IsSosMps(settings.SpeedOfSound_mps)
                ? settings.SpeedOfSound_mps
                : double.NaN;

            AddressMask = settings.AddressMask;
            _x_offset_m = settings.AntennaXOffset_m;
            _y_offset_m = settings.AntennaYOffset_m;
        }

        private void InitializeFilters(AZMSettings settings)
        {
            _usblDhFifoSize = settings.USBL_DHFilter_FIFO_Size;
            _usblDhMaxSpeed = settings.USBL_DHFilter_MaxSpeed_mps;
            _usblDhThreshold = settings.USBL_DHFilter_Threshold_m;
            _usblSFifoSize = settings.USBL_SFilter_FIFO_Size;
            _usblSThreshold = settings.USBL_SFilter_Threshold_m;

            if (settings.LBL_Use_DHFilter)
                _lblDHFilter = new DHTrackFilterCartesian(
                    settings.LBL_DHFilter_FIFO_Size,
                    settings.LBL_DHFilter_MaxSpeed_mps,
                    settings.LBL_DHFilter_Threshold_m);

            if (settings.LBL_Use_SFilter)
                _lblSFilter = new TrackMovingAverageSmootherCartesian(
                    settings.LBL_SFilter_FIFO_Size,
                    settings.LBL_SFilter_Threshold_m);

            _lblRErrThreshold = settings.LBL_RErr_Threshold_m;
        }

        private void InitializeLBL(AZMSettings settings)
        {
            if (settings.LBLResponderCoordinatesMode == LBLResponderCoordinatesModeEnum.Cartesian)
            {
                Set3RespondersLocalCoordinates(
                    settings.LBL_R1_X, settings.LBL_R1_Y,
                    settings.LBL_R2_X, settings.LBL_R2_Y,
                    settings.LBL_R3_X, settings.LBL_R3_Y);
            }
            else if (settings.LBLResponderCoordinatesMode == LBLResponderCoordinatesModeEnum.Geographic)
            {
                Set3RespondersGeographicCoordinates(
                    settings.LBL_R1_X, settings.LBL_R1_Y,
                    settings.LBL_R2_X, settings.LBL_R2_Y,
                    settings.LBL_R3_X, settings.LBL_R3_Y);
            }
        }

        private void InitializeOutputs(OutputSettings settings)
        {
            var fmt = settings.IsPSIMSSBEnabled
                ? (IOutputFormatter)new PSIMSSBFormatter()
                : new RawProtocolFormatter();

            _output = new OutputManager(fmt);

            _output.OnLineGenerated += line => OnLineGenerated?.Invoke(line);

            if (settings.SerialEnabled && !string.IsNullOrEmpty(settings.SerialPortName))
                _output.AddChannel(new SerialOutputChannel("serial_out",
                    settings.SerialPortName, settings.SerialBaudrate));

            if (settings.UDPEnabled && settings.UDPBroadcastEndpoint != null)
                _output.AddChannel(new UDPOutputChannel("udp_out", settings.UDPBroadcastEndpoint));

            if (settings.BeaconEndpoints?.Count > 0)
            {
                var perBeacon = new UDPPerBeaconChannel("per_beacon");
                _output.SetPerBeaconChannel(perBeacon);
                foreach (var kvp in settings.BeaconEndpoints)
                    perBeacon.SetBeaconEndpoint(kvp.Key, kvp.Value);
            }
        }

        #endregion

        #region Public control commands

        public bool PauseInterrogation()
        {
            return _azmPort.Query_BaseStart(0, Salinity_PSU, MaxDist_m);
        }

        public bool ResumeInterrogation()
        {
            return _azmPort.Query_BaseStart(AddressMask, Salinity_PSU, MaxDist_m);
        }

        public bool RequestDataFromBeacon(REMOTE_ADDR_Enum addr, CDS_REQ_CODES_Enum dataID)
        {
            return _azmPort.Query_CREQ(addr, dataID);
        }

        public void SetBeaconUDPOutput(REMOTE_ADDR_Enum addr, IPEndPoint ep)
        {
            if (_output.PerBeaconChannel == null)
            {
                var ch = new UDPPerBeaconChannel("per_beacon");
                _output.SetPerBeaconChannel(ch);
            }
            _output.PerBeaconChannel?.SetBeaconEndpoint(addr, ep);
        }

        public bool QueryLocalAddress()
        {
            return _azmPort.Query_RSTS(0, double.NaN);
        }

        public bool QueryLocalAddressSet(REMOTE_ADDR_Enum address)
        {
            return _azmPort.Query_RSTS(address, double.NaN);
        }

        public void Disconnect()
        {
            PauseInterrogation();
            _azmPort.Stop();
        }

        #region Output management

        /// <summary>
        /// Установить serial output канал
        /// </summary>
        public void SetSerialOutput(string portName, BaudRate baudRate)
        {
            // Удаляем старый serial канал если есть
            _output.RemoveChannel("serial_out");

            if (!string.IsNullOrEmpty(portName) && portName.ToUpper() != "OFF")
                _output.AddChannel(new SerialOutputChannel("serial_out", portName, baudRate));
        }

        /// <summary>
        /// Установить UDP broadcast output канал
        /// </summary>
        public void SetUDPOutput(IPEndPoint endpoint)
        {
            _output.RemoveChannel("udp_out");

            if (endpoint != null)
                _output.AddChannel(new UDPOutputChannel("udp_out", endpoint));
        }

        /// <summary>
        /// Установить индивидуальный UDP для маяка
        /// </summary>
        public void SetBeaconUDPOutput(int addr, IPEndPoint? endpoint)
        {
            var beaconAddr = (REMOTE_ADDR_Enum)(addr);

            if (endpoint == null)
            {
                _output.PerBeaconChannel?.RemoveBeacon(beaconAddr);
                return;
            }

            if (_output.PerBeaconChannel == null)
            {
                var ch = new UDPPerBeaconChannel("per_beacon");
                _output.SetPerBeaconChannel(ch);
            }
            _output.PerBeaconChannel?.SetBeaconEndpoint(beaconAddr, endpoint);
        }

        #endregion


        #region Runtime settings

        /// <summary>
        /// Обновить маску адресов 
        /// </summary>
        public bool UpdateAddressMask(ushort newMask)
        {
            AddressMask = newMask;
            return true;
        }

        /// <summary>
        /// Обновить солёность (без перезапуска опроса)
        /// </summary>
        public void UpdateSalinity(double newSalinity_PSU)
        {
            if (!AZM.IsStyPSU(newSalinity_PSU))
                throw new ArgumentOutOfRangeException(nameof(newSalinity_PSU));
            Salinity_PSU = newSalinity_PSU;
        }

        /// <summary>
        /// Обновить максимальную дистанцию 
        /// </summary>
        public bool UpdateMaxDistance(double newMaxDist_m)
        {
            if (!AZM.IsMaxDst(newMaxDist_m))
                throw new ArgumentOutOfRangeException(nameof(newMaxDist_m));
            MaxDist_m = newMaxDist_m;
            return true;
        }

        /// <summary>
        /// Обновить солёность И макс. дистанцию 
        /// </summary>
        public bool UpdateSalinityAndMaxDistance(double newSalinity_PSU, double newMaxDist_m)
        {
            if (!AZM.IsStyPSU(newSalinity_PSU))
                throw new ArgumentOutOfRangeException(nameof(newSalinity_PSU));
            if (!AZM.IsMaxDst(newMaxDist_m))
                throw new ArgumentOutOfRangeException(nameof(newMaxDist_m));

            Salinity_PSU = newSalinity_PSU;
            MaxDist_m = newMaxDist_m;

            return true;
        }

        /// <summary>
        /// Обновить оффсеты антенны
        /// </summary>
        public void UpdateAntennaOffsets(double x_m, double y_m, double phi_deg)
        {
            if (!AZM.IsHdnDeg(phi_deg))
                throw new ArgumentOutOfRangeException(nameof(phi_deg));
            _x_offset_m = x_m;
            _y_offset_m = y_m;
            _phi_deg = phi_deg;
        }

        /// <summary>
        /// Обновить скорость звука (без перезапуска опроса)
        /// </summary>
        public void UpdateSoundSpeed(double sos_mps)
        {
            SpeedOfSound_mps = AZM.IsSosMps(sos_mps) ? sos_mps : double.NaN;
        }

        /// <summary>
        /// Обновить задержку данных (для CompassHistory)
        /// </summary>
        public void UpdateDataLatency(double latencyMs)
        {
            _dataLatencyMs = latencyMs;
        }

        /// <summary>
        /// Применить все накопившиеся изменения и перезапустить опрос
        /// </summary>
        public bool ApplyRuntimeChanges()
        {
            if (_azmPort.Status == AuxStatus.Detected && _azmPort.IsDeviceInfoValid)
                return _azmPort.Query_BaseStart(AddressMask, Salinity_PSU, MaxDist_m);
            return true;
        }

        public bool ApplyAndRestart()
        {
            return ResumeInterrogation();
        }

        #region Location Override

        private System.Timers.Timer? _overrideTimer;
        private double _overrideLat, _overrideLon, _overrideHdg;
        private bool _overrideActive;

        public bool LocationOverrideActive => _overrideActive;

        /// <summary>
        /// Переопределить координаты и курс вручную (Location Override).
        /// Значения будут обновляться раз в секунду, пока не вызван DisableLocationOverride.
        /// </summary>
        public void OverrideLocation(double lat_deg, double lon_deg, double heading_deg)
        {
            if (!AZM.IsLatDeg(lat_deg)) throw new ArgumentOutOfRangeException(nameof(lat_deg));
            if (!AZM.IsLonDeg(lon_deg)) throw new ArgumentOutOfRangeException(nameof(lon_deg));
            if (!AZM.IsHdnDeg(heading_deg)) throw new ArgumentOutOfRangeException(nameof(heading_deg));

            _overrideLat = lat_deg;
            _overrideLon = lon_deg;
            _overrideHdg = heading_deg;

            if (_overrideTimer == null)
            {
                _overrideTimer = new System.Timers.Timer(1000) { AutoReset = true };
                _overrideTimer.Elapsed += (_, _) =>
                {
                    State.Lat_deg.Value = _overrideLat;
                    State.Lon_deg.Value = _overrideLon;
                    State.Heading_deg.Value = _overrideHdg;
                };
            }

            if (!_overrideActive)
            {
                _overrideTimer.Start();
                _overrideActive = true;
            }
        }

        /// <summary>
        /// Отключить ручное переопределение координат.
        /// </summary>
        public void DisableLocationOverride()
        {
            _overrideTimer?.Stop();
            _overrideActive = false;
        }

        #endregion

        #endregion


        #endregion

        #region Angular calibration

        public void AngularCalibrationStart(double fromAngle, double toAngle,
            double step, int nPoints, REMOTE_ADDR_Enum beaconAddr)
        {
            if (AngularCalibration)
            {
                AngularCalibration = false;
                _acManager.Reset();
            }

            _acStAngle = fromAngle;
            _acNdAngle = toAngle;
            _acStep = step;
            _acNPoints = nPoints;
            _acBeaconAddr = beaconAddr;

            _acManager.X_Offset_m = _x_offset_m;
            _acManager.Y_Offset_m = _y_offset_m;

            AngularCalibration = true;
        }

        public void AngularCalibrationStop()
        {
            AngularCalibration = false;
        }

        public void LoadCalibrationTable(double[] encoderAngles, double[] errors)
        {
            _antCorrector.LoadCalibration(encoderAngles, errors);
        }

        #endregion

        #region LBL

        public bool Set3RespondersLocalCoordinates(double r1x, double r1y,
            double r2x, double r2y, double r3x, double r3y)
        {
            if (double.IsNaN(r1x) || double.IsNaN(r1y) ||
                double.IsNaN(r2x) || double.IsNaN(r2y) ||
                double.IsNaN(r3x) || double.IsNaN(r3y))
                return false;

            Remotes.GetOrAdd(REMOTE_ADDR_Enum.REM_ADDR_1, _ => new ResponderBeacon(REMOTE_ADDR_Enum.REM_ADDR_1)).X_m.Value = r1x;
            Remotes[REMOTE_ADDR_Enum.REM_ADDR_1].Y_m.Value = r1y;

            Remotes.GetOrAdd(REMOTE_ADDR_Enum.REM_ADDR_2, _ => new ResponderBeacon(REMOTE_ADDR_Enum.REM_ADDR_2)).X_m.Value = r2x;
            Remotes[REMOTE_ADDR_Enum.REM_ADDR_2].Y_m.Value = r2y;

            Remotes.GetOrAdd(REMOTE_ADDR_Enum.REM_ADDR_3, _ => new ResponderBeacon(REMOTE_ADDR_Enum.REM_ADDR_3)).X_m.Value = r3x;
            Remotes[REMOTE_ADDR_Enum.REM_ADDR_3].Y_m.Value = r3y;

            return true;
        }

        public bool Set3RespondersGeographicCoordinates(double r1lon, double r1lat,
            double r2lon, double r2lat, double r3lon, double r3lat)
        {
            if (!AZM.IsLonDeg(r1lon) || !AZM.IsLatDeg(r1lat) ||
                !AZM.IsLonDeg(r2lon) || !AZM.IsLatDeg(r2lat) ||
                !AZM.IsLonDeg(r3lon) || !AZM.IsLatDeg(r3lat))
                return false;

            _lblProcessor.RefPointLat = r1lat;
            _lblProcessor.RefPointLon = r1lon;

            double rplat = Algorithms.Deg2Rad(r1lat);
            double rplon = Algorithms.Deg2Rad(r1lon);

            Algorithms.GetDeltasByGeopoints_WGS84(rplat, rplon,
                Algorithms.Deg2Rad(r2lat), Algorithms.Deg2Rad(r2lon), out double r2x, out double r2y);
            Algorithms.GetDeltasByGeopoints_WGS84(rplat, rplon,
                Algorithms.Deg2Rad(r3lat), Algorithms.Deg2Rad(r3lon), out double r3x, out double r3y);

            return Set3RespondersLocalCoordinates(0, 0, r2x, r2y, r3x, r3y);
        }

        public bool Discard3RespondersCoordinates()
        {
            _lblProcessor.RefPointLat = double.NaN;
            _lblProcessor.RefPointLon = double.NaN;

            foreach (var addr in new[] { REMOTE_ADDR_Enum.REM_ADDR_1, REMOTE_ADDR_Enum.REM_ADDR_2, REMOTE_ADDR_Enum.REM_ADDR_3 })
            {
                if (Remotes.TryGetValue(addr, out var r))
                {
                    r.X_m.DeInit();
                    r.Y_m.DeInit();
                }
            }
            return true;
        }

        #endregion

        #region AZM Port events

        private void SubscribeToAZMEvents()
        {
            _azmPort.OnStatusChanged += (_, _) =>
            {
                if (_azmPort.Status == AuxStatus.Detected)
                    LogInfo($"AZM Detected on {_azmPort.PortName}");
                else if (_azmPort.Status == AuxStatus.Inactive)
                    LogInfo("AZM Stopped");
            };

            _azmPort.DeviceInfoValidChanged += (_, _) =>
            {
                if (_azmPort.IsDeviceInfoValid &&
                    (_azmPort.DeviceType == AZM_DEVICE_TYPE_Enum.DT_USBL_TSV ||
                     _azmPort.DeviceType == AZM_DEVICE_TYPE_Enum.DT_LBL_TSV))
                {
                    LogInfo($"Starting polling (AddrMask={AddressMask}, MaxDist={MaxDist_m}m)");
                    _azmPort.Query_BaseStart(AddressMask, Salinity_PSU, MaxDist_m);
                }

                if (_azmPort.IsDeviceInfoValid)
                {
                    DeviceType = _azmPort.DeviceType;
                    DeviceSerialNumber = _azmPort.SerialNumber;
                    DeviceVersionInfo = $"{_azmPort.SystemInfo} v{_azmPort.SystemVersion}";
                }
                else
                {
                    DeviceType = AZM_DEVICE_TYPE_Enum.DT_INVALID;
                    DeviceSerialNumber = string.Empty;
                    DeviceVersionInfo = string.Empty;
                }
            };

            _azmPort.STRSTPReceived += (_, e) =>
            {
                InterrogationActive = e.AddrMask != 0;

                if (e.AddrMask == 0)
                    LogInfo("Interrogation paused");
                else
                    LogInfo($"Polling started (AddrMask={e.AddrMask}, MaxDist={e.MaxDist_m}m)");
            };

            _azmPort.NDTAReceived += (_, e) =>
            {
                ProcessStationLocalData(e);

                if (e.Status == NDTA_Status_Enum.NDTA_REMT)
                    SetRemoteTimeout(e.Address);
                else if (e.Status == NDTA_Status_Enum.NDTA_REMR)
                {
                    if (DeviceType == AZM_DEVICE_TYPE_Enum.DT_USBL_TSV)
                        ProcessRemoteUSBL(e);
                    else if (DeviceType == AZM_DEVICE_TYPE_Enum.DT_LBL_TSV)
                        ProcessRemoteLBL(e);
                }
            };

            _azmPort.LogEventHandler += (_, e) => LogEventHandler?.Invoke(this, e);
        }

        #endregion

        #region Aux data handling

        private void OnAuxDataUpdated(object? sender, EventArgs e)
        {
            if (_auxData.Latitude.HasValue)
                State.Lat_deg.Value = _auxData.Latitude.Value;
            if (_auxData.Longitude.HasValue)
                State.Lon_deg.Value = _auxData.Longitude.Value;
            if (_auxData.Heading.HasValue)
            {
                State.Heading_deg.Value = _auxData.Heading.Value;
                _compassHistory.Add(_auxData.Heading.Value);
            }
            if (_auxData.Course.HasValue)
                State.Course_deg.Value = _auxData.Course.Value;
            if (_auxData.Speed.HasValue)
                State.Speed_mps.Value = _auxData.Speed.Value;
            if (_auxData.Depth.HasValue)
                State.StDepth_m.Value = _auxData.Depth.Value;
        }

        #endregion

        #region NDTA processing

        private void ProcessStationLocalData(NDTAReceivedEventArgs e)
        {
            if (!double.IsNaN(e.LocTemp_C))
            {
                State.WaterTemp_C.Value = e.LocTemp_C;
                _wpManager.Temperature = e.LocTemp_C;
            }

            if (!double.IsNaN(e.LocPrs_mBar))
            {
                State.StPressure_mBar.Value = e.LocPrs_mBar;
                _wpManager.Pressure = e.LocPrs_mBar;

                if (State.WaterTemp_C.IsInitialized)
                {
                    double density = PHX.Water_density_calc(
                        State.WaterTemp_C.Value, e.LocPrs_mBar, Salinity_PSU);
                    State.StDepth_m.Value = PHX.Depth_by_pressure_calc(
                        e.LocPrs_mBar, PHX.PHX_ATM_PRESSURE_MBAR, density, PHX.PHX_GRAVITY_ACC_MPS2);
                    State.Z_m.Value = State.StDepth_m.Value;
                }
            }

            if (!double.IsNaN(e.LocPitch_deg)) State.StPitch_deg.Value = e.LocPitch_deg;
            if (!double.IsNaN(e.LocRoll_deg)) State.StRoll_deg.Value = e.LocRoll_deg;

            _output.OnStationData(State, DateTime.UtcNow);
        }

        private void SetRemoteTimeout(REMOTE_ADDR_Enum address)
        {
            var remote = Remotes.GetOrAdd(address, _ => new ResponderBeacon(address));
            remote.IsTimeout = true;
            remote.Timeouts++;

            if (remote.Azimuth_deg.IsInitialized && remote.SRangeProjection_m.IsInitialized)
                _output.OnBeaconData(remote, State, DateTime.UtcNow);
        }

        private void ProcessRemoteUSBL(NDTAReceivedEventArgs e)
        {
            var remote = Remotes.GetOrAdd(e.Address, _ => new ResponderBeacon(e.Address));
            remote.IsTimeout = false;
            remote.SuccededRequests++;

            ProcessBeaconCommonData(remote, e, out bool hasProjection);

            if (!double.IsNaN(e.HAngle_deg))
                remote.Azimuth_deg.Value = _antCorrector.CorrectAngle(e.HAngle_deg);
            if (!double.IsNaN(e.VAngle_deg))
                remote.Elevation_deg.Value = e.VAngle_deg;

            if (remote.Azimuth_deg.IsInitialized && hasProjection)
            {
                if (State.Lat_deg.IsInitializedAndNotObsolete &&
                    State.Lon_deg.IsInitializedAndNotObsolete &&
                    State.Heading_deg.IsInitializedAndNotObsolete)
                {
                    double cHeading = _compassHistory.GetHeadingAtOffset(_dataLatencyMs);

                    if (AngularCalibration && e.Address == _acBeaconAddr)
                    {
                        _acManager.AddPoint(DateTime.Now, cHeading,
                            State.Lat_deg.Value, State.Lon_deg.Value,
                            remote.Azimuth_deg.Value, remote.SRangeProjection_m.Value,
                            remote.PTime_s.Value, State.StDepth_m.Value, remote.Depth_m.Value);

                        if (_acManager.Count >= _acNPoints)
                        {
                            _phi_deg = _acManager.Calibrate_Phi(_acStAngle, _acNdAngle, _acStep);
                            AngularCalibration = false;
                            LogInfo($"Angular calibration finished: phi={_phi_deg:F01}°");
                        }
                    }

                    AZMMath.PolarCS_ShiftRotate(cHeading, _phi_deg,
                        remote.Azimuth_deg.Value, remote.SRangeProjection_m.Value,
                        _x_offset_m, _y_offset_m, out double aAzm, out double aRng);

                    AZMMath.CalculateAbsLocationDirectGeodetic(
                        Algorithms.Deg2Rad(State.Lat_deg.Value),
                        Algorithms.Deg2Rad(State.Lon_deg.Value),
                        Algorithms.Deg2Rad(aAzm), aRng,
                        out double rlatRad, out double rlonRad, out _);

                    DateTime ts = DateTime.Now;

                    remote.DHFilterState ??= new DHTrackFilter(
                        _usblDhFifoSize, _usblDhMaxSpeed, _usblDhThreshold);

                    if (remote.DHFilterState.Process(rlatRad, rlonRad, 0, ts,
                            out rlatRad, out rlonRad, out _, out _))
                    {
                        if (USBLRawDataEventEnabled)
                        {
                            USBLRawDataHandler?.Invoke(this, new USBLRawDataEventArgs(
                                e.Address, ts, e.HAngle_deg, e.VAngle_deg,
                                e.PropTime_s, e.SlantRange_m, e.SlantRangeProjection_m,
                                e.MSR_dB, State.StDepth_m.Value));
                        }

                        remote.TFilterState ??= new TrackMovingAverageSmoother(
                            _usblSFifoSize, _usblSThreshold);

                        double rdpt = remote.Depth_m.IsInitialized ? remote.Depth_m.Value : 0;
                        remote.TFilterState.Process(rlatRad, rlonRad, rdpt, DateTime.Now,
                            out rlatRad, out rlonRad, out _, out _);

                        remote.AAzimuth_deg.Value = aAzm;
                        remote.RAzimuth_deg.Value = Algorithms.Wrap360(aAzm + 180);
                        remote.ADistance_m.Value = aRng;
                        remote.Lat_deg.Value = Algorithms.Rad2Deg(rlatRad);
                        remote.Lon_deg.Value = Algorithms.Rad2Deg(rlonRad);
                    }
                }
                else
                {
                    remote.RAzimuth_deg.Value = Algorithms.Wrap360(remote.Azimuth_deg.Value + 180);
                }
            }

            _output.OnBeaconData(remote, State, DateTime.UtcNow);
        }

        private void ProcessRemoteLBL(NDTAReceivedEventArgs e)
        {
            var remote = Remotes.GetOrAdd(e.Address, _ => new ResponderBeacon(e.Address));
            remote.IsTimeout = false;
            remote.SuccededRequests++;

            ProcessBeaconCommonData(remote, e, out bool hasProjection);

            if (hasProjection && remote.X_m.IsInitialized && remote.Y_m.IsInitialized && remote.Z_m.IsInitialized)
            {
                _lblProcessor.UpdatePoint(e.Address,
                    remote.X_m.Value, remote.Y_m.Value, remote.Z_m.Value,
                    remote.SRangeProjection_m.Value);

                if (_lblProcessor.CanFormNavigationBase())
                {
                    var basePoints = _lblProcessor.GetValidPointsForSolver();

                    double xPrev = State.X_m.IsInitialized ? State.X_m.Value : 0;
                    double yPrev = State.Y_m.IsInitialized ? State.Y_m.Value : 0;

                    if (!State.X_m.IsInitialized)
                    {
                        xPrev = basePoints.Average(p => p.X);
                        yPrev = basePoints.Average(p => p.Y);
                    }

                    Algorithms.TOA_NLM2D_Solve(basePoints.ToArray(), xPrev, yPrev, State.Z_m.Value,
                        Algorithms.NLM_DEF_IT_LIMIT, Algorithms.NLM_DEF_PREC_THRLD, 1.0,
                        out double xCurr, out double yCurr, out double rerr, out _);

                    if (rerr <= _lblRErrThreshold)
                    {
                        double rx = xCurr, ry = yCurr;

                        if (_lblDHFilter?.Process(rx, ry, State.Z_m.Value, DateTime.Now,
                                out rx, out ry, out _, out _) ?? true)
                        {
                            _lblSFilter?.Process(rx, ry, State.Z_m.Value, DateTime.Now,
                                out rx, out ry, out _, out _);

                            State.X_m.Value = rx;
                            State.Y_m.Value = ry;
                            State.Rerr_m.Value = rerr;

                            if (_lblProcessor.IsRefPoint)
                            {
                                Algorithms.GeopointOffsetByDeltas_WGS84(
                                    Algorithms.Deg2Rad(_lblProcessor.RefPointLat),
                                    Algorithms.Deg2Rad(_lblProcessor.RefPointLon),
                                    ry, rx, out double latRad, out double lonRad);
                                State.Lat_deg.Value = Algorithms.Rad2Deg(latRad);
                                State.Lon_deg.Value = Algorithms.Rad2Deg(lonRad);
                            }
                        }
                    }
                }
            }

            _output.OnBeaconData(remote, State, DateTime.UtcNow);
        }

        private void ProcessBeaconCommonData(ResponderBeacon remote, NDTAReceivedEventArgs e, out bool hasProjection)
        {
            hasProjection = false;

            if (!double.IsNaN(e.MSR_dB)) remote.MSR_dB.Value = e.MSR_dB;

            if (!double.IsNaN(e.RemotesDepth_m))
            {
                remote.Depth_m.Value = e.RemotesDepth_m;
                remote.Z_m.Value = e.RemotesDepth_m;
            }

            if (!double.IsNaN(e.PropTime_s))
            {
                remote.PTime_s.Value = e.PropTime_s;

                if (IsRecalculateRange)
                {
                    hasProjection = true;
                    if (DeviceType == AZM_DEVICE_TYPE_Enum.DT_LBL_TSV && IsPTimeAdjustment)
                        remote.SRange_m.Value = (e.PropTime_s - _pTimePerAddrAdjustment -
                            _pTimePerAddrAdjustment2 * (int)e.Address) * _wpManager.SoundSpeed;
                    else
                        remote.SRange_m.Value = e.PropTime_s * _wpManager.SoundSpeed;

                    if (State.StDepth_m.IsInitialized && remote.Depth_m.IsInitialized)
                        remote.SRangeProjection_m.Value = AZMMath.TryCalculateSlantRangeProjection(
                            State.StDepth_m.Value, remote.Depth_m.Value, remote.SRange_m.Value);
                    else
                        remote.SRangeProjection_m.Value = remote.SRange_m.Value;
                }
            }

            if (!hasProjection)
            {
                if (!double.IsNaN(e.SlantRangeProjection_m))
                {
                    remote.SRangeProjection_m.Value = e.SlantRangeProjection_m;
                    hasProjection = true;
                }
                else if (!double.IsNaN(e.SlantRange_m))
                {
                    remote.SRangeProjection_m.Value = e.SlantRange_m;
                    hasProjection = true;
                }
            }
        }

        #endregion

        #region Logging

        private void LogInfo(string msg) =>
            LogEventHandler?.Invoke(this, new LogEventArgs(LogLineType.INFO, msg));

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _overrideTimer?.Stop();
            _overrideTimer?.Dispose();
            _output.Dispose();
        }

        #endregion

        #region Events

        public EventHandler<LogEventArgs>? LogEventHandler;
        public EventHandler? InterrogationActiveChangedHandler;
        public EventHandler<USBLRawDataEventArgs>? USBLRawDataHandler;
        public event Action<string>? OnLineGenerated;

        #endregion
    }
}