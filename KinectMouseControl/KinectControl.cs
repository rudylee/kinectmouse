using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using Coding4Fun.Kinect.Wpf;
using Microsoft.Kinect;
using Microsoft.Samples.Kinect.SwipeGestureRecognizer;

namespace KinectMouseControl
{
    class KinectControl
    {
        private readonly KinectSensor _kinect;
        private readonly double _layoutWidth;
        private readonly double _layoutHeight;
        private bool _mouseClick = false;
        private readonly Queue<double> _weightedX = new Queue<double>();
        private readonly Queue<double> _weightedY = new Queue<double>();

        private readonly Recognizer _activeRecognizer;


        public KinectControl(double layoutHeight, double layoutWidth)
        {
            _layoutHeight = layoutHeight;
            _layoutWidth = layoutWidth;

            _kinect = KinectSensor.KinectSensors.FirstOrDefault();
            _kinect.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;

            if (_kinect != null)
            {
                _kinect.Start();

                _kinect.ColorStream.Enable();
                _kinect.SkeletonStream.Enable(new TransformSmoothParameters
                {
                    Smoothing = 0.7f,
                    Correction = 0.1f,
                    Prediction = 0.1f,
                    JitterRadius = 0.05f,
                    MaxDeviationRadius = 0.05f
                });

                _activeRecognizer = CreateRecognizer();
                _kinect.SkeletonFrameReady += kinect_SkeletonFrameReady;
            }

        }

        private Recognizer CreateRecognizer()
        {
            // Instantiate a recognizer.
            var recognizer = new Recognizer();

            // Wire-up swipe right to manually advance picture.
            recognizer.SwipeRightDetected += (s, e) => DoMouseClick();

            // Wire-up swipe right to manually advance picture.
            recognizer.SwipeLeftDetected += (s, e) => DoMouseClick();

            return recognizer;
        }

        private static Skeleton GetPrimarySkeleton(IEnumerable<Skeleton> skeletons)
        {
            Skeleton primarySkeleton = null;
            foreach (var skeleton in skeletons.Where(skeleton => skeleton.TrackingState == SkeletonTrackingState.Tracked))
            {
                if (primarySkeleton == null)
                    primarySkeleton = skeleton;
                else if (primarySkeleton.Position.Z > skeleton.Position.Z)
                    primarySkeleton = skeleton;
            }
            return primarySkeleton;
        }

        private void kinect_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = null;

            using (SkeletonFrame frame = e.OpenSkeletonFrame())
            {
                if (frame != null)
                {
                    skeletons = new Skeleton[frame.SkeletonArrayLength];
                    frame.CopySkeletonDataTo(skeletons);
                }
            }

            if (skeletons == null) return;

            foreach (var skeleton in skeletons)
            {
                if (skeleton.TrackingState == SkeletonTrackingState.Tracked)
                {
                    var rightHand = skeleton.Joints[JointType.HandRight];
                    var leftHand = skeleton.Joints[JointType.HandLeft];

                    var rightHandScaled = rightHand.ScaleTo((int)_layoutWidth , (int)_layoutHeight  , 0.2f, 0.4f);
                    var leftHandScaled = leftHand.ScaleTo((int)_layoutWidth , (int)_layoutHeight  , 0.2f, 0.4f);

                    //rightHandScaled  = ExponentialWeightedAvg( scaledJoint );

                    var handX = rightHandScaled.Position.X;
                    var handY = rightHandScaled.Position.Y;

                    var leftHandX = leftHandScaled.Position.X;
                    var leftHandY = leftHandScaled.Position.Y;

                    SetPosition(Convert.ToInt32(handX), Convert.ToInt32(handY));

                    if (leftHandX > 100 && _mouseClick == false)
                    {
                        DoMouseClick();
                        _mouseClick = true;
                    }

                    if (leftHandX < 100)
                    {
                        _mouseClick = false;
                    }
                }
            }
        }

        public double ExponentialMovingAverage(double[] data, double baseValue)
        {
            double numerator = 0;
            double denominator = 0;

            double average = data.Sum();
            average /= data.Length;

            for (int i = 0; i < data.Length; ++i)
            {
                numerator += data[i] * Math.Pow(baseValue, data.Length - i - 1);
                denominator += Math.Pow(baseValue, data.Length - i - 1);
            }

            numerator += average * Math.Pow(baseValue, data.Length);
            denominator += Math.Pow(baseValue, data.Length);

            return numerator / denominator;
        }

        public double WeightedAverage(double[] data, double[] weights)
        {
            if (data.Length != weights.Length)
            {
                return Double.MinValue;
            }

            double weightedAverage = data.Select((t, i) => t * weights[i]).Sum();

            return weightedAverage / weights.Sum();
        }

        private Point ExponentialWeightedAvg(Joint joint)
        {
            _weightedX.Enqueue(joint.Position.X);
            _weightedY.Enqueue(joint.Position.Y);

            if (_weightedX.Count > 0.7f)
            {
                _weightedX.Dequeue();
                _weightedY.Dequeue();
            }

            double x = ExponentialMovingAverage(_weightedX.ToArray(), 0.9);
            double y = ExponentialMovingAverage(_weightedY.ToArray(), 0.9);

            return new Point(x, y);
        }

        private void SetPosition(int a, int b)
        {
            SetCursorPos(a, b);
        }

        [DllImport("User32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        public static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);

        public const int MOUSEEVENTF_LEFTDOWN = 0x02;
        public const int MOUSEEVENTF_LEFTUP = 0x04;

        public static void DoMouseClick()
        {
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
        }

    }
}
