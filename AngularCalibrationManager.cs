using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UCNLNav;
using UCNLNav.TrackFilters;

namespace AZMLib
{
    internal class AngularCalibrationManager
    {
        public double X_Offset_m { get; set; } = 0.0;
        public double Y_Offset_m { get; set; } = 0.0;

        public double Phi_deg { get; set; } = 0.0;

        private readonly List<CalibrationPoint> _cPoints = new List<CalibrationPoint>();
        private readonly DHTrackFilter _dhFilter = new DHTrackFilter(8, 1, 5);
        private readonly TrackMovingAverageSmoother _sFilter = new TrackMovingAverageSmoother(4, 50);

        public int Count { get => _cPoints.Count; }

        public void AddPoint(DateTime ts, double heading_deg, double lat_deg, double lon_deg, double azm_deg, double srp_m, double ptime_s, double adpt_m, double rdpt_m)
        {
            _cPoints.Add(new CalibrationPoint
            {
                TS = ts,
                Hdn_deg = heading_deg,
                Lat_deg = lat_deg,
                Lon_deg = lon_deg,
                Azm_deg = azm_deg,
                SRP_m = srp_m,
                PTm_s = ptime_s,
                ADpt_m = adpt_m,
                RDpt_m = rdpt_m
            });
        }

        public double Calibrate_Phi(double fromAngle_deg, double toAngle_deg, double angleStep_deg)
        {

            double drms_best = int.MaxValue;
            double phi_best = fromAngle_deg;

            foreach (var phi_deg in GetAngles(fromAngle_deg, toAngle_deg, angleStep_deg))
            {
                List<GeoPoint> points = new List<GeoPoint>();
                _dhFilter.Reset();
                _sFilter.Reset();
                foreach (var cp in _cPoints)
                {
                    if (CalculateResponderPosition(cp, X_Offset_m, Y_Offset_m, phi_deg, out double rlat_deg, out double rlon_deg))
                    {
                        points.Add(new GeoPoint(rlat_deg, rlon_deg));
                    }
                }

                var mpoints = Navigation.GCSToLCS(points, Algorithms.WGS84Ellipsoid);
                Navigation.GetPointsSTD2D(mpoints, out double sigmax, out double sigmay);
                var drms = Navigation.DRMS(sigmax, sigmay);

                if (drms < drms_best)
                {
                    drms_best = drms;
                    phi_best = phi_deg;
                }
            }

            return phi_best;
        }

        public double Calibrate_XY(double xwidth, double ywidth, double step)
        {
            double drms_best = int.MaxValue;
            double x_best = 0;
            double y_best = 0;

            double x = -xwidth / 2;

            do
            {
                double y = -ywidth / 2;
                do
                {




                    y += step;
                } while (y < ywidth / 2);

                x += step;
            } while (x < xwidth / 2);

            throw new NotImplementedException();

        }


        public void Reset()
        {
            _cPoints.Clear();
            _dhFilter.Reset();
            _sFilter.Reset();
        }
        
        private static IEnumerable<double> GetAngles(double stAngle_deg, double enAngle_deg, double step)
        {
            stAngle_deg = ((stAngle_deg % 360) + 360) % 360;
            enAngle_deg = ((enAngle_deg % 360) + 360) % 360;

            if (enAngle_deg <= stAngle_deg)
                enAngle_deg += 360;

            double cAngle_deg = stAngle_deg;
            while (cAngle_deg <= enAngle_deg + step / 2)
            {
                yield return cAngle_deg % 360;
                cAngle_deg += step;
            }
        }

        private bool CalculateResponderPosition(CalibrationPoint cp, double offsx, double offsy, double phi_deg, out double rlat_deg, out double rlon_deg)
        {
            bool result = false;

            rlat_deg = cp.Lat_deg;
            rlon_deg = cp.Lon_deg;

            AZMMath.PolarCS_ShiftRotate(
                cp.Hdn_deg,
                phi_deg,
                cp.Azm_deg,
                cp.SRP_m,
                offsx, offsy,
                out double a_azm, out double a_rng);

            AZMMath.CalculateAbsLocationDirectGeodetic(
                Algorithms.Deg2Rad(cp.Lat_deg),
                Algorithms.Deg2Rad(cp.Lon_deg),
                Algorithms.Deg2Rad(a_azm), a_rng,
                out double rlat_rad,
                out double rlon_rad,
                out double _);

            if (_dhFilter.Process(rlat_rad, rlon_rad, 0, cp.TS,
                out rlat_rad, out rlon_rad, out _, out _))
            {
                double rdpt_m = cp.RDpt_m;

                _sFilter.Process(
                    rlat_rad, rlon_rad, rdpt_m, cp.TS,
                    out rlat_rad, out rlon_rad, out _, out _);

                rlat_deg = Algorithms.Rad2Deg(rlat_rad);
                rlon_deg = Algorithms.Rad2Deg(rlon_rad);

                result = true;
            }

            return result;
        }

        private struct CalibrationPoint
        {
            public DateTime TS;

            public double Hdn_deg;
            public double Lat_deg;
            public double Lon_deg;

            public double Azm_deg;
            public double SRP_m;
            public double PTm_s;
            public double ADpt_m;
            public double RDpt_m;
        }
    }
}