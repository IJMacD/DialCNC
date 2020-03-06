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
using static DialCNC.Controller;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace DialCNC
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        Controller cnc_controller;

        private ObservableCollection<string> Data { get; } = new ObservableCollection<string>();

        readonly RadialController radial_controller;

        private double mmPerRotation = 10;

        public MainPage()
        {
            this.InitializeComponent();

            cnc_controller = new Controller();

            radial_controller = RadialController.CreateForCurrentView();

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

            radial_controller.Menu.Items.Add(xAxisItem);
            radial_controller.Menu.Items.Add(yAxisItem);
            radial_controller.Menu.Items.Add(zAxisItem);

            radial_controller.UseAutomaticHapticFeedback = true;
            radial_controller.RotationResolutionInDegrees = 5;

            radial_controller.RotationChanged += Controller_RotationChanged;
            radial_controller.ButtonClicked += Controller_ButtonClicked;

            // foreach (var name in SerialPort.GetPortNames())
            // {
            //    port_select.Items.Add(name);
            // }

            port_select.SelectedIndex = 0;

            connect_button.Click += Connect_button_Click;
            stop_button.Click += Stop_button_Click;
            //send_button.Click += Send_button_Click;
            unlock_button.Click += Unlock_button_Click;

            left_button.Click += Left_button_Click;
            right_button.Click += Right_button_Click;

            x_set_text.LostFocus += X_set_text_LostFocus;
            y_set_text.LostFocus += Y_set_text_LostFocus;
            z_set_text.LostFocus += Z_set_text_LostFocus;

            live_checkBox.Checked += Live_checkBox_Checked;
            live_checkBox.Unchecked += Live_checkBox_Unchecked;
            go_button.Click += Go_button_Click;
            to_origin_button.Click += To_origin_button_Click;
            to_work_origin_button.Click += To_work_origin_button_Click;
            zero_work_button.Click += Zero_work_button_Click;

            cnc_controller.StatusChanged += Cnc_controller_StatusChanged;
            cnc_controller.PositionChanged += Cnc_controller_PositionChanged;
        }

        private void Zero_work_button_Click(object sender, RoutedEventArgs e)
        {
            cnc_controller.ZeroWork();
        }

        private void To_origin_button_Click(object sender, RoutedEventArgs e)
        {
            cnc_controller.MoveAbsolute(0, 0, 0);
        }

        private void To_work_origin_button_Click(object sender, RoutedEventArgs e)
        {
            cnc_controller.MoveAbsolute(0, 0, 0);
        }

        private void Cnc_controller_PositionChanged(object sender, EventArgs e)
        {
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                string mx = cnc_controller.MachinePosition.X.ToString("0.00");
                string my = cnc_controller.MachinePosition.Y.ToString("0.00");
                string mz = cnc_controller.MachinePosition.Z.ToString("0.00");

                x_get_text.Text = mx;
                y_get_text.Text = my;
                z_get_text.Text = mz;

                string wx = cnc_controller.WorkPosition.X.ToString("0.00");
                string wy = cnc_controller.WorkPosition.Y.ToString("0.00");
                string wz = cnc_controller.WorkPosition.Z.ToString("0.00");

                x_work_text.Text = wx;
                y_work_text.Text = wy;
                z_work_text.Text = wz;

                if (live_checkBox.IsChecked.Value)
                {
                    if (x_set_text.FocusState == FocusState.Unfocused) x_set_text.Text = wx;
                    if (y_set_text.FocusState == FocusState.Unfocused) y_set_text.Text = wy;
                    if (z_set_text.FocusState == FocusState.Unfocused) z_set_text.Text = wz;
                }
            });
        }

        private void Cnc_controller_StatusChanged(object sender, EventArgs e) {
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                status_text.Text = cnc_controller.Status == ControllerStatus.Idle ? "Idle" : (cnc_controller.Status == ControllerStatus.Run ? "Run" : (cnc_controller.Status == ControllerStatus.Alarm ? "Alarm" : "Unknown"));
            });
        }

        private void Go_button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var x = double.Parse(x_set_text.Text);
                var y = double.Parse(y_set_text.Text);
                var z = double.Parse(z_set_text.Text);
                cnc_controller.MoveAbsolute(x, y, z);
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
                    cnc_controller.MoveAbsolute(Axes.X, v);
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
                    cnc_controller.MoveAbsolute(Axes.Y, v);
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
                    cnc_controller.MoveAbsolute(Axes.Z, v);
                }
                catch (ArgumentNullException) { }
                catch (FormatException) { }
            }
        }

        private void Connect_button_Click(object sender, RoutedEventArgs e)
        {
            if (cnc_controller.IsConnected)
            {
                cnc_controller.Disconnect();
            }
            else
            {
                cnc_controller.Connect();
            }
        }

        private void Unlock_button_Click(object sender, RoutedEventArgs e)
        {
            cnc_controller.Unlock();
        }

        private void Stop_button_Click(object sender, RoutedEventArgs e)
        {
            cnc_controller.Stop();
        }

        private void Controller_ButtonClicked(RadialController sender, RadialControllerButtonClickedEventArgs args)
        {
            cnc_controller.SpindleRunning = !cnc_controller.SpindleRunning;
        }

        private void Controller_RotationChanged(RadialController sender, RadialControllerRotationChangedEventArgs args)
        {
            var menuItem = sender.Menu.GetSelectedMenuItem();

            cnc_controller.Move((Axes)menuItem.Tag, args.RotationDeltaInDegrees * mmPerRotation / 360);
        }

        //private void Send_button_Click(object sender, RoutedEventArgs e)
        //{
        //    p?.WriteLine(input_text.Text);
        //}

        private void Right_button_Click(object sender, RoutedEventArgs e)
        {
            cnc_controller.Move(Axes.X, 1);
        }

        private void Left_button_Click(object sender, RoutedEventArgs e)
        {
            cnc_controller.Move(Axes.X, -1);
        }

        
    }
}
