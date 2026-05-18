// AZMLib/AZMSettings.cs
using System.Net;
using UCNLDrivers;

namespace AZMLib
{
    public enum LBLResponderCoordinatesModeEnum
    {
        None = 0,
        Cartesian = 1,
        Geographic = 2,
        Invalid
    }
    public class AZMSettings : SimpleSettingsContainer
    {
        #region Main parameters

        public ushort AddressMask = 1;
        public int MaxDist_m = 1000;
        public double Salinity_PSU = 0.0;
        public double SpeedOfSound_mps = double.NaN;

        public double AntennaXOffset_m = 0.0;
        public double AntennaYOffset_m = 0.0;
        public double AntennaAngularOffset_deg = 0.0;

        #endregion

        #region USBL Filters

        public double USBL_DHFilter_MaxSpeed_mps = 1.0;
        public double USBL_DHFilter_Threshold_m = 5.0;
        public int USBL_DHFilter_FIFO_Size = 8;
        public double USBL_SFilter_Threshold_m = 20.0;
        public int USBL_SFilter_FIFO_Size = 4;

        #endregion

        #region LBL

        public LBLResponderCoordinatesModeEnum LBLResponderCoordinatesMode = LBLResponderCoordinatesModeEnum.None;

        public double LBL_R1_X = double.NaN;
        public double LBL_R1_Y = double.NaN;
        public double LBL_R2_X = double.NaN;
        public double LBL_R2_Y = double.NaN;
        public double LBL_R3_X = double.NaN;
        public double LBL_R3_Y = double.NaN;

        public double LBL_RErr_Threshold_m = 25.0;
        public bool LBL_Use_DHFilter = false;
        public double LBL_DHFilter_MaxSpeed_mps = 1.0;
        public double LBL_DHFilter_Threshold_m = 5.0;
        public int LBL_DHFilter_FIFO_Size = 8;
        public bool LBL_Use_SFilter = false;
        public double LBL_SFilter_Threshold_m = 30.0;
        public int LBL_SFilter_FIFO_Size = 4;

        #endregion

        #region SimpleSettingsContainer

        public override void SetDefaults()
        {
            AddressMask = 1;
            MaxDist_m = 1000;
            Salinity_PSU = 0.0;
            SpeedOfSound_mps = double.NaN;

            AntennaXOffset_m = 0.0;
            AntennaYOffset_m = 0.0;
            AntennaAngularOffset_deg = 0.0;

            USBL_DHFilter_MaxSpeed_mps = 1.0;
            USBL_DHFilter_Threshold_m = 5.0;
            USBL_DHFilter_FIFO_Size = 8;
            USBL_SFilter_Threshold_m = 20.0;
            USBL_SFilter_FIFO_Size = 4;

            LBLResponderCoordinatesMode = LBLResponderCoordinatesModeEnum.None;
            LBL_R1_X = double.NaN; LBL_R1_Y = double.NaN;
            LBL_R2_X = double.NaN; LBL_R2_Y = double.NaN;
            LBL_R3_X = double.NaN; LBL_R3_Y = double.NaN;

            LBL_RErr_Threshold_m = 25.0;
            LBL_Use_DHFilter = false;
            LBL_DHFilter_MaxSpeed_mps = 1.0;
            LBL_DHFilter_Threshold_m = 5.0;
            LBL_DHFilter_FIFO_Size = 8;
            LBL_Use_SFilter = false;
            LBL_SFilter_Threshold_m = 30.0;
            LBL_SFilter_FIFO_Size = 4;
        }

        #endregion
    }
}