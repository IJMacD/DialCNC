using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Devices.SerialCommunication;
using Windows.Devices.Enumeration;
using System.IO.Ports;
using System.Collections.ObjectModel;
using Windows.UI.Input;
using Windows.Storage.Streams;
using System.Text;
using System.Threading.Tasks;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace DialCNC
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        private SerialPort p;

        private bool connected;

        private ObservableCollection<string> Data { get; } = new ObservableCollection<string>();

        readonly RadialController controller;

        private double mmPerRotation = 10;

        private bool spindle;

        private enum Axes
        {
            X,
            Y,
            Z,
        }

        public MainPage()
        {
            this.InitializeComponent();

            controller = RadialController.CreateForCurrentView();

            RadialControllerConfiguration myConfiguration = RadialControllerConfiguration.GetForCurrentView();
            myConfiguration.SetDefaultMenuItems(new[] {
                RadialControllerSystemMenuItemKind.Volume,
                RadialControllerSystemMenuItemKind.NextPreviousTrack
            });

            RandomAccessStreamReference xicon = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/XAxis.64.png"));
            RandomAccessStreamReference yicon = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/YAxis.64.png"));
            RandomAccessStreamReference zicon = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/ZAxis.64.png"));

            RadialControllerMenuItem xAxisItem = RadialControllerMenuItem.CreateFromIcon("X-Axis", xicon);
            RadialControllerMenuItem yAxisItem = RadialControllerMenuItem.CreateFromIcon("Y-Axis", yicon);
            RadialControllerMenuItem zAxisItem = RadialControllerMenuItem.CreateFromIcon("Z-Axis", zicon);

            xAxisItem.Tag = Axes.X;
            yAxisItem.Tag = Axes.Y;
            zAxisItem.Tag = Axes.Z;

            controller.Menu.Items.Add(xAxisItem);
            controller.Menu.Items.Add(yAxisItem);
            controller.Menu.Items.Add(zAxisItem);

            controller.UseAutomaticHapticFeedback = true;
            controller.RotationResolutionInDegrees = 5;

            controller.RotationChanged += Controller_RotationChanged;
            controller.ButtonClicked += Controller_ButtonClicked;

            // foreach (var name in SerialPort.GetPortNames())
            // {
            //    port_select.Items.Add(name);
            // }

            port_select.SelectedIndex = 0;

            connect_button.Click += Connect_button_Click;
            stop_button.Click += Stop_button_Click;
            send_button.Click += Send_button_Click;
            unlock_button.Click += Unlock_button_Click;

            left_button.Click += Left_button_Click;
            right_button.Click += Right_button_Click;

            x_set_text.LostFocus += X_set_text_LostFocus;
            y_set_text.LostFocus += Y_set_text_LostFocus;
            z_set_text.LostFocus += Z_set_text_LostFocus;

            live_checkBox.Checked += Live_checkBox_Checked;
            live_checkBox.Unchecked += Live_checkBox_Unchecked;
            go_button.Click += Go_button_Click;
        }

        private void Go_button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var x = double.Parse(x_set_text.Text);
                var y = double.Parse(y_set_text.Text);
                var z = double.Parse(z_set_text.Text);
                MoveAbsolute(x, y, z);
            }
            catch (ArgumentNullException) { }
            catch (FormatException) { }
        }

        private void Live_checkBox_Checked(object sender, RoutedEventArgs e)
        {
            go_button.IsEnabled = false;
        }

        private void Live_checkBox_Unchecked(object sender, RoutedEventArgs e)
        {
            go_button.IsEnabled = true;
        }

        private void X_set_text_LostFocus(object sender, RoutedEventArgs e)
        {
            if (live_checkBox.IsChecked.HasValue && live_checkBox.IsChecked.Value)
            {
                try
                {
                    var v = double.Parse(((TextBox)sender).Text);
                    MoveAbsolute(Axes.X, v);
                }
                catch (ArgumentNullException) { }
                catch (FormatException) { }
            }
        }
        private void Y_set_text_LostFocus(object sender, RoutedEventArgs e)
        {
            if (live_checkBox.IsChecked.HasValue && live_checkBox.IsChecked.Value)
            {
                try
                {
                    var v = double.Parse(((TextBox)sender).Text);
                    MoveAbsolute(Axes.Y, v);
                }
                catch (ArgumentNullException) { }
                catch (FormatException) { }
            }
        }

        private void Z_set_text_LostFocus(object sender, RoutedEventArgs e)
        {
            if (live_checkBox.IsChecked.HasValue && live_checkBox.IsChecked.Value)
            {
                try
                {
                    var v = double.Parse(((TextBox)sender).Text);
                    MoveAbsolute(Axes.Z, v);
                }
                catch (ArgumentNullException) { }
                catch (FormatException) { }
            }
        }

        private void Connect_button_Click(object sender, RoutedEventArgs e)
        {
            if (connected)
            {
                disconnect();
            }
            else
            {
                connect();
            }
        }

        private void disconnect()
        {
            connected = false;
            connect_button.Content = "Connect";
            p.Close();
        }

        private void connect()
        {
            try
            {
                if (p == null)
                {
                    p = new SerialPort("COM6");
                }

                p.BaudRate = 115200;

                p.Open();

                connected = true;
                connect_button.Content = "Disconnect";

                p.DataReceived += S_DataReceived;
                p.ErrorReceived += P_ErrorReceived;

                input_text.IsEnabled = true;
                send_button.IsEnabled = true;

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
                Data.Add("Error: " + ex.Message);
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
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => Data.Add(data));
            }
        }

        private void ParseStatusLine(string data)
        {
            var parts = data.Split(',');

            var x = parts[1].Split(':')[1];
            var y = parts[2];
            var z = parts[3];

            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                x_get_text.Text = x;
                y_get_text.Text = y;
                z_get_text.Text = z;

                if (live_checkBox.IsChecked.Value)
                {
                    if(x_set_text.FocusState == FocusState.Unfocused) x_set_text.Text = x;
                    if (y_set_text.FocusState == FocusState.Unfocused) y_set_text.Text = y;
                    if (z_set_text.FocusState == FocusState.Unfocused) z_set_text.Text = z;
                }
            });
        }

        private void Unlock_button_Click(object sender, RoutedEventArgs e)
        {
            p?.WriteLine("$X");
        }

        private void Stop_button_Click(object sender, RoutedEventArgs e)
        {
            char stopCode = (char)0x85;
            p?.WriteLine(stopCode.ToString());
        }

        private void Controller_ButtonClicked(RadialController sender, RadialControllerButtonClickedEventArgs args)
        {
            spindle = !spindle;
            p?.WriteLine(spindle ? "M3 S100" : "M5");
        }

        private void Controller_RotationChanged(RadialController sender, RadialControllerRotationChangedEventArgs args)
        {
            var menuItem = sender.Menu.GetSelectedMenuItem();

            Move((Axes)menuItem.Tag, args.RotationDeltaInDegrees * mmPerRotation / 360);
        }

        private void Send_button_Click(object sender, RoutedEventArgs e)
        {
            p?.WriteLine(input_text.Text);
        }

        private void Right_button_Click(object sender, RoutedEventArgs e)
        {
            Move(Axes.X, 1);
        }

        private void Left_button_Click(object sender, RoutedEventArgs e)
        {
            Move(Axes.X, -1);
        }

        private void Move(Axes a, double v)
        {
            var a_str = a == Axes.X ? "X" : (a == Axes.Y ? " Y" : "Z");
            p?.WriteLine("G91G0" + a_str + v);
        }

        private void MoveAbsolute(Axes a, double v)
        {
            var a_str = a == Axes.X ? "X" : (a == Axes.Y ? " Y" : "Z");
            p?.WriteLine("G90G0" + a_str + v);
        }

        private void MoveAbsolute(double x, double y, double z)
        {
            p?.WriteLine("G90G0X" + x + "Y" + y + "Z" + z);
        }

        public static async Task SetTimeout(Action action, TimeSpan timeout)
        {
            await Task.Delay(timeout).ConfigureAwait(false);

            action();
        }

        
    }
}
