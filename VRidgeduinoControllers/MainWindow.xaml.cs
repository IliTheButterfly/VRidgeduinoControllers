﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using VRidge = VRE.Vridge.API.Client;
using VRE.Vridge.API.Client;
//using Microsoft.Kinect;
using Accord;
using Accord.Math;
using K = Microsoft.Kinect;
using VRidgeduinoControllers.MathUtilities;
using VRidgeduinoControllers.Services;
using VRidgeduinoControllers.Remotes;

namespace VRidgeduinoControllers
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        VRidge.Remotes.VridgeRemote Remote;
        SafeControllerRemote LeftRemote;
        SafeControllerRemote RightRemote;
        SafeHeadRemote Head;
        private const int listenPort = 7000;
        VRidgeduinoCommunicationService CommunicationService;
        KinectService Kinect;
        Thread RemoteHandler;

        bool VRidgeHeadOK
        {
            get
            {
                try
                {
                    return Remote != null && Remote.Head != null &&
                        !Remote.Head.IsDisposed;

                }
                catch (ObjectDisposedException)
                {

                }
                return false;
            }
        }

        bool VRidgeControllersOK
        {
            get
            {
                try
                {
                    return Remote != null && Remote.Controller != null &&
                        !Remote.Controller.IsDisposed;

                }
                catch (ObjectDisposedException)
                {

                }
                return false;
            }
        }
         

        public MainWindow()
        {
            InitializeComponent();
            CommunicationService = new VRidgeduinoCommunicationService();
            CommunicationService.Start();
            Kinect = new KinectService();
            RemoteHandler = new Thread(RemoteRoutine);
            RemoteHandler.Start();
        }

        private void CommunicationService_LeftControllerUpdateAvailable(object sender, ControllerUpdateInfo e)
        {
            LeftRemote.Info = e;
            Dispatcher.Invoke(() =>
            {
                LeftTrigLabel.Content = $"Trig: {e.Trig}";
                LeftGripLabel.Content = $"Grip: {e.Grip}";
                LeftAnalogLabel.Content = $"X: {e.AnalogX} Y: {e.AnalogY}";
                LeftStickLabel.Content = $"Stick: {e.TouchPress}";
                LeftQuaternionLabel.Content = $"Quaternion: {e.Rotation}";
                LeftBatteryVoltageLabel.Content = $"Battery: {e.Battery}";
            });
        }


        void VRidgeStateDisplay()
        {
            Dispatcher.Invoke(() =>
            {
                VRidgeStateLabel.Content = !VRidgeHeadOK && !VRidgeControllersOK ? "VRidge off" :
                    VRidgeHeadOK && VRidgeControllersOK ? "VRidge on" :
                    !VRidgeControllersOK ? "VRidge controllers off" :
                    "VRidge head off";
            });
        }

        void RemoteRoutine()
        {
            try
            {
                using (Remote = 
                    new VRidge.Remotes.VridgeRemote("localhost", 
                    "VRidgeduinoControllers", 
                    VRidge.Remotes.Capabilities.Controllers | 
                    VRidge.Remotes.Capabilities.HeadTracking))
                {
                    LeftRemote = new SafeControllerRemote(Remote, 
                        VRidge.Messages.BasicTypes.HandType.Left);
                    RightRemote = new SafeControllerRemote(Remote, 
                        VRidge.Messages.BasicTypes.HandType.Right);
                    Head = new SafeHeadRemote(Remote);

                    CommunicationService.LeftControllerUpdateAvailable += 
                        CommunicationService_LeftControllerUpdateAvailable;
                    CommunicationService.RightControllerUpdateAvailable += 
                        CommunicationService_RightControllerUpdateAvailable;
                    Kinect.PositionFrameReady += Kinect_PositionFrameReady;

                    Head.Position = new Vector3(0f, 2.5f, 1.3f);
                    LeftRemote.Position = new Vector3(-0.2f, 2.2f, 1f);
                    RightRemote.Position = new Vector3(0.2f, 2.2f, 1f);
                    int i = 0;
                    while (true)
                    {
                        if (i++ == 100)
                        {
                            VRidgeStateDisplay();
                            i = 0;
                        }
                        if (LeftRemote.TryUpdateController())
                        {
                            Dispatcher.Invoke(() =>
                            {
                                LeftConvertedQuaternionLabel.Content = LeftRemote.ConvertedRotation;
                                LeftEuler.Content = LeftRemote.EulerRotation;
                            });
                        }
                        if (RightRemote.TryUpdateController())
                        {
                            Dispatcher.Invoke(() =>
                            {
                                RightConvertedQuaternionLabel.Content = RightRemote.ConvertedRotation;
                                RightEuler.Content = RightRemote.EulerRotation;
                            });
                        }
                        Head.TryUpdateHead();
                        DebugFeatures();
                        Thread.Sleep(5);
                    }
                }
            }
            catch (ThreadAbortException)
            {

            }
        }

        private void Kinect_PositionFrameReady(object sender, PositionFrame e)
        {
            LeftRemote.Position = e.LeftPos;
            RightRemote.Position = e.RightPos;
            Head.Position = e.HeadPos;
        }

        private enum DebugState
        {
            None,
            DebugSelectPressed,
            DebugSelectReleased,
        }
        DebugState _state;
        private void DebugFeatures()
        {
            if (_state == DebugState.None && RightRemote.Info.Grip && RightRemote.Info.Trig)
            {
                _state = DebugState.DebugSelectPressed;
            }
            else if (_state == DebugState.DebugSelectPressed && !RightRemote.Info.Trig && !RightRemote.Info.Grip)
            {
                _state = DebugState.DebugSelectReleased;
            }
            else if (_state == DebugState.DebugSelectReleased)
            {
                if (LeftRemote.Info.Grip)
                {
                    _state = DebugState.None;
                    LeftRemote.ResetRotation();
                }

                else if (RightRemote.Info.Grip)
                {
                    _state = DebugState.None;
                    RightRemote.ResetRotation();
                }

                else if (RightRemote.Info.Trig)
                {
                    _state = DebugState.None;
                    Head.TryResetPosition();
                }
            }
        }

        private void CommunicationService_RightControllerUpdateAvailable(object sender, ControllerUpdateInfo e)
        {
            RightRemote.Info = e;
            Dispatcher.Invoke(() =>
            {
                RightTrigLabel.Content = $"Trig: {e.Trig}";
                RightGripLabel.Content = $"Grip: {e.Grip}";
                RightAnalogLabel.Content = $"X: {e.AnalogX} Y: {e.AnalogY}";
                RightStickLabel.Content = $"Stick: {e.TouchPress}";
                RightQuaternionLabel.Content = $"Quaternion: {e.Rotation}";
                RightBatteryVoltageLabel.Content = $"Battery: {e.Battery}";
            });
        }


        private void Window_Closed(object sender, EventArgs e)
        {
            CommunicationService.Stop();
            Kinect.Dispose();
            RemoteHandler.Abort();
        }

        //private Vector4 CreateRotation(double x, double y, double z)
        //{
        //    var euler = VRidgeduinoMath.ToRadiansEuler(new Vector3((float)x, (float)y, (float)z));

        //    return System.Numerics.Quaternion.CreateFromYawPitchRoll(euler.X, euler.Y, euler.Z).ToAccord();
        //}

        private Vector3 CreateOffset(double x, double y, double z)
        {
            return VRidgeduinoMath.ToRadiansEuler(new Vector3((float)x, (float)y, (float)z));
        }

        private void RightXSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            //RightRemote.ConvertedRotation = CreateRotation(RightXSlider.Value, RightYSlider.Value, RightZSlider.Value);
            RightRemote.Offset = CreateOffset(RightXSlider.Value, RightYSlider.Value, RightZSlider.Value);
        }

        private void RightYSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            //RightRemote.ConvertedRotation = CreateRotation(RightXSlider.Value, RightYSlider.Value, RightZSlider.Value);
            RightRemote.Offset = CreateOffset(RightXSlider.Value, RightYSlider.Value, RightZSlider.Value);

        }

        private void RightZSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            //RightRemote.ConvertedRotation = CreateRotation(RightXSlider.Value, RightYSlider.Value, RightZSlider.Value);
            RightRemote.Offset = CreateOffset(RightXSlider.Value, RightYSlider.Value, RightZSlider.Value);
        }
    }
}
