using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using WiiDesktop.Exceptions;
using WiiDesktop.Domain.Calibration;
using WiimoteLib;
using WiiDesktop.View;
using System.Windows.Forms;
using WiiDesktop.Common;

namespace WiiDesktop.Controller
{
    public class VirtualDesktop : Subject
    {
        private bool isCalibrated;
        private bool wasCalibrated;
        private bool calibrating;
        private int screenWidth;
        private int screenHeight;
        private Mutex mx;
        private Wiimote wiimote;
        private WiimoteController controller;
        private Calibrator calibrator;
        //Almacena la �ltima informaci�n de calibraci�n creada durante
        //la ejecuci�n de la aplicaci�n.
        private CalibrationData lastCalibrationData;

        public VirtualDesktop()
        {
            isCalibrated = false;
            wasCalibrated = false;
            calibrating = false;
            screenWidth = Screen.PrimaryScreen.Bounds.Width;
            screenHeight = Screen.PrimaryScreen.Bounds.Height;

            mx = new Mutex();
            controller = new WiimoteController(screenWidth, screenHeight);

            wiimote = new Wiimote();
            wiimote.WiimoteChanged += new WiimoteChangedEventHandler(OnWiimoteChanged);
        }

        public Point StartCalibration()
        {
            calibrator = new Calibrator(screenWidth, screenHeight);
            //Guardo el �ltimo estado de la calibraci�n
            wasCalibrated = isCalibrated;
            isCalibrated = false;
            calibrating = true;
            return new Point(calibrator.GetX(), calibrator.GetY());
        }

        //Este m�todo debe llamarse s�lo si el proceso de calibraci�n es cancelado
        public void StopCalibration()
        {
            mx.WaitOne();

            //Se detiene el proceso de calibraci�n
            calibrating = false;
            //Se restaura el estado que ten�a antes de empezar la calibraci�n
            isCalibrated = wasCalibrated;

            mx.ReleaseMutex();
        }

        public Boolean LoadCalibration()
        {
            try
            {
                CalibrationData data = CalibrationPersister.LoadCalibrationData();
                if (calibrator == null)
                {
                    calibrator = new Calibrator(screenWidth, screenHeight);
                }
                calibrator.Calibrate(data);
                isCalibrated = true;
                return true;
            }
            catch (CalibrationDataNotFoundException)
            {
                return false;
            }
        }

        public void StartDesktop()
        {
            try
            {
                wiimote.Connect();
                wiimote.SetReportType(Wiimote.InputReport.IRAccel, true);
                wiimote.SetLEDs(true, false, false, false);
            }
            catch (System.Exception x)
            {
                throw new UserTerminatedException("WiimoteLib.dll:" + x.Message + "\nWiimoteIRDetector: Failed while trying to connect to Wiimote.");
            }
        }

        public void StopDesktop()
        {
            if (IsCalibrated() && (lastCalibrationData != null))
            {
                CalibrationPersister.SaveCalibrationData(lastCalibrationData);
            }
            wiimote.SetReportType(Wiimote.InputReport.IRAccel, false);
            wiimote.SetLEDs(false, false, false, false);
            wiimote.Disconnect();
        }

        public bool IsCalibrated()
        {
            return isCalibrated;
        }

        public float GetX()
        {
            return calibrator.GetX();
        }

        public float GetY()
        {
            return calibrator.GetY();
        }

        public int GetBatteryCharge()
        {
            return (int)wiimote.WiimoteState.Battery / 2;
        }

        private void OnWiimoteChanged(object sender, WiimoteChangedEventArgs args)
        {
            mx.WaitOne();

            try
            {
                if (isCalibrated)
                {
                    controller.StartHandling(args.WiimoteState);
                }
                else if (calibrating)
                {
                    isCalibrated = calibrator.Calibrate(args.WiimoteState);
                    calibrating = !isCalibrated;
                    if (isCalibrated)
                    {
                        lastCalibrationData = calibrator.GetCalibrationData();
                    }
                    if (calibrator.HasChanged())
                    {
                        Notify();
                    }
                }

            }
            catch (UserTerminatedException)
            {
                wiimote.Disconnect();
            }

            mx.ReleaseMutex();
        }

    }
}
