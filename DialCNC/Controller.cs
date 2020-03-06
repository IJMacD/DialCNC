using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace DialCNC
{
    class Controller
    {
        public enum ControllerStatus
        {
            Unknown,
            Idle,
            Run,
            Alarm,
        };

        public enum Axes
        {
            X,
            Y,
            Z,
        }

        public Vector3 MachinePosition;
        public Vector3 WorkPosition;

        private SerialPort p;

        private bool connected;

        private bool spindle;

        public ControllerStatus Status = ControllerStatus.Unknown;

        public event EventHandler StatusChanged;

        public class StatusChangedEventArgs : EventArgs
        {
            ControllerStatus Status;
            public StatusChangedEventArgs(ControllerStatus status) { Status = status; }
        }

        public event EventHandler PositionChanged;

        public bool IsConnected
        {
            get { return connected; }
        }

        public void Connect()
        {
            try
            {
                if (p == null)
                {
                    p = new SerialPort("COM6");
                }

                p.BaudRate = 115200;

                p.Open();

                p.NewLine = "\r\n";

                connected = true;
                //connect_button.Content = "Disconnect";

                p.DataReceived += S_DataReceived;
                p.ErrorReceived += P_ErrorReceived;

                //input_text.IsEnabled = true;
                //send_button.IsEnabled = true;

                Action runPositionLoop = null;

                runPositionLoop = () =>
                {
                    QueryPosition();

                    if (connected)
                    {
                        _ = SetTimeout(runPositionLoop, TimeSpan.FromMilliseconds(100));
                    }
                };

                runPositionLoop();

            }
            catch (Exception ex)
            {
                //Data.Add("Error: " + ex.Message);
            }
        }
        public void Disconnect()
        {
            connected = false;
            //connect_button.Content = "Connect";
            p.Close();
            p = null;
        }

        public bool SpindleRunning
        {
            get { return spindle; }
            set
            {
                spindle = value;
                p?.WriteLine(value ? "M3 S100" : "M5");
            }
        }

        private void QueryPosition()
        {
            p.WriteLine("?");
        }

        private void P_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            connected = false;
        }

        private async void S_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            var data = p.ReadLine();

            if (data[0] == '<')
            {
                ParseStatusLine(data);
            }
            else
            {
                //await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => Data.Add(data));
            }
        }

        private void ParseStatusLine(string data)
        {
            var parts = data.Substring(1, data.Length - 2).Split(',');

            ControllerStatus oldStatus = Status;

            switch (parts[0])
            {
                case "Idle": Status = ControllerStatus.Idle; break;
                case "Run": Status = ControllerStatus.Run; break;
                case "Alarm": Status = ControllerStatus.Alarm; break;
                default: Status = ControllerStatus.Unknown; break;
            }

            if (oldStatus != Status)
            {
                StatusChanged?.Invoke(this, new StatusChangedEventArgs(Status));
            }

            var mx = parts[1].Split(':')[1];
            var my = parts[2];
            var mz = parts[3];

            MachinePosition.X = float.Parse(mx);
            MachinePosition.Y = float.Parse(my);
            MachinePosition.Z = float.Parse(mz);

            var wx = parts[4].Split(':')[1];
            var wy = parts[5];
            var wz = parts[6];

            WorkPosition.X = float.Parse(wx);
            WorkPosition.Y = float.Parse(wy);
            WorkPosition.Z = float.Parse(wz);

            PositionChanged?.Invoke(this, null);
        }


        public void Move(Axes a, double v)
        {
            var a_str = a == Axes.X ? "X" : (a == Axes.Y ? " Y" : "Z");
            p?.WriteLine("G91G0" + a_str + v);
        }

        public void MoveAbsolute(Axes a, double v)
        {
            var a_str = a == Axes.X ? "X" : (a == Axes.Y ? " Y" : "Z");
            p?.WriteLine("G90G0" + a_str + v);
        }

        public void MoveAbsolute(double x, double y, double z)
        {
            p?.WriteLine("G90G0X" + x + "Y" + y + "Z" + z);
        }

        //public void MoveMachineAbsolute(double x, double y, double z)
        //{
        //    p?.WriteLine("G90G0X" + x + "Y" + y + "Z" + z);
        //}

        public void ZeroWork ()
        {
            p?.WriteLine("G92X0Y0Z0");
        }

        public static async Task SetTimeout(Action action, TimeSpan timeout)
        {
            await Task.Delay(timeout).ConfigureAwait(false);

            action();
        }

        internal void Stop()
        {
            char stopCode = (char)0x85;
            p?.WriteLine(stopCode.ToString());
        }

        internal void Unlock()
        {
            p?.WriteLine("$X");
        }
    }
}
