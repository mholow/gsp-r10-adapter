namespace Recording
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using OpenCvSharp;
    using OpenCvSharp.Extensions;

    using Size = OpenCvSharp.Size;

    public class FrameQueue<T> : ConcurrentQueue<T>
    {
        private readonly object syncObject = new object();

        public int Size { get; private set; }

        public FrameQueue(int size)
        {
            Size = size;
        }

        public T[] GetLastNFrames(int n)
        {
            int start = 0;
            if (n < base.Count)
            {
                start = base.Count - n;
            }
            Console.WriteLine($"N = {n} Start = {start} Count = {base.Count}");
            return base.ToArray()[(start)..];
        }

        public new void Enqueue(T obj)
        {
            base.Enqueue(obj);
            lock (syncObject)
            {
                while (base.Count > Size)
                {
                    T outObj;
                    base.TryDequeue(out outObj);
                }
            }
        }
    }

    public class Recorder : IDisposable
    {
        private readonly VideoCaptureAPIs _videoCaptureApi = VideoCaptureAPIs.DSHOW;
        private readonly ManualResetEventSlim _threadStopEvent = new(false);
        private readonly VideoCapture _videoCapture;
        private Thread _captureThread;
        private Thread _displayThread;
        private FrameQueue<Mat> frameQ;

        private int Thing = 100;

        private Mat[] frames = new Mat[0];

        private bool IsVideoCaptureValid => _videoCapture is not null && _videoCapture.IsOpened();

        public Recorder(int deviceIndex, int frameWidth, int frameHeight, double fps)
        {
            _videoCapture = VideoCapture.FromCamera(deviceIndex, _videoCaptureApi);
            _videoCapture.Open(deviceIndex, _videoCaptureApi);

            _videoCapture.FrameWidth = frameWidth;
            _videoCapture.FrameHeight = frameHeight;
            _videoCapture.Fps = fps;

            frameQ = new FrameQueue<Mat>(1000);

            if (!IsVideoCaptureValid)
                Console.WriteLine("Vid cap not ready");


            //_videoWriter = new VideoWriter(path, FourCC.XVID, _videoCapture.Fps, new Size(_videoCapture.FrameWidth, _videoCapture.FrameHeight));

            _threadStopEvent.Reset();

            _captureThread = new Thread(CaptureFrameLoop);
            _captureThread.Start();

            _displayThread = new Thread(DisplayLoop);
            _displayThread.Start();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        ~Recorder()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopRecording();

                _videoCapture?.Release();
                _videoCapture?.Dispose();
            }
        }

        public void PlayLastNSeconds(int n)
        {
            frames = frameQ.GetLastNFrames((int)(n * _videoCapture.Fps));
        }

        public void StopRecording()
        {
            _threadStopEvent.Set();

            _captureThread?.Join();
            _captureThread = null;

            _displayThread?.Join();
            _displayThread = null;

            _threadStopEvent.Reset();


        }

        private void CaptureFrameLoop()
        {
            while (!_threadStopEvent.Wait(0))
            {
                Mat frame = new Mat();
                _videoCapture.Read(frame);
                frameQ.Enqueue(frame);
            }
        }

        private void DisplayLoop()
        {
            //Cv2.NamedWindow("test");
            //Cv2.CreateTrackbar("Speed", "test", ref Thing, 500);

            while (!_threadStopEvent.Wait(0))
            {
                if (frames.Length == 0) Thread.Sleep(500);
                for (int i = 0; i < frames.Length; i++)
                {
                    frames[i].PutText("aaa", new OpenCvSharp.Point(100, 100), HersheyFonts.HersheyPlain, 1, Scalar.Aqua);
                    Cv2.ImShow("test", frames[i]);
                    Cv2.WaitKey(Thing + 1);

                    if (Cv2.GetWindowProperty("test", WindowPropertyFlags.Visible) < 1) break;
                }

            }
        }
    }

}