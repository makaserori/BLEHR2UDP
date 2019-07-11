using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Serialization.Json;
using System.Windows;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;
using Rug.Osc;
using System.Net;
using System.IO;
using Microsoft.Win32;
using System.Text;
using System.Net.Sockets;


namespace HR_BLE_to_UDP
{
    public class SensorTagUuid
    {
        // https://sensortag.codeplex.com/SourceControl/latest#SensorTagLibrary/SensorTagLibrary/Source/SensorTagUuid.cs
        //public const string UuidIrtService = @"180D";
        //public const string UuidIrtString = @"2A37";
        public const string UuidIrtService = "0000180D-0000-1000-8000-00805F9B34FB";
        public const string UuidIrtData = "00002A37-0000-1000-8000-00805F9B34FB";
        // public const string UuidIrtConf = "00002A39-0000-1000-8000-00805F9B34FB";
    }


    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private GattDeviceService GattDeviceService { get; set; }

        private GattCharacteristic GattCharacteristic { get; set; }
        IPAddress sendAddress = null;
        int count = 0;
        private UdpClient client;

        //ファイル書き出し用
        Stream st = null;
        StreamWriter sw = null;
        String delmiter = ",";  //CSVだから

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void ButtonConnect_Click(object sender, RoutedEventArgs e)
        {
            // SensorTagを取得
            var selector = GattDeviceService.GetDeviceSelectorFromUuid(new Guid(SensorTagUuid.UuidIrtService));
            var devices = await DeviceInformation.FindAllAsync(selector);
            var deviceInformation = devices.FirstOrDefault();
            if (deviceInformation == null)
            {
                MessageBox.Show("not found");
                return;
            }

            GattDeviceService = await GattDeviceService.FromIdAsync(deviceInformation.Id);
            MessageBox.Show($"found {deviceInformation.Id}");
        }

        private async void ButtonReadValue_Click(object sender, RoutedEventArgs e)
        {
            if (GattDeviceService == null)
            {
                MessageBox.Show("Please click connect button");
                return;
            }

            // 値を読み始める
            if (GattCharacteristic == null)
            {
#pragma warning disable CS0618 // Type or member is obsolete
                GattCharacteristic = GattDeviceService.GetCharacteristics(new Guid(SensorTagUuid.UuidIrtData)).First();
#pragma warning restore CS0618 // Type or member is obsolete
                GattCharacteristic.ValueChanged += GattCharacteristic_ValueChanged;

                var status = await GattCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                if (status == GattCommunicationStatus.Unreachable)
                {
                    MessageBox.Show("Failed");
                }

            }
        }

        private async void GattCharacteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            // 値を読んで表示する
            await Dispatcher.InvokeAsync(() =>
            {
                var data = new byte[args.CharacteristicValue.Length];
                DataReader.FromBuffer(args.CharacteristicValue).ReadBytes(data);
                textBlock1.Text = "Sample No. " + count.ToString() + "\n HR: " + data.GetValue(1).ToString();
                count++;

                if (checkBoxUDP.IsChecked == true)
                {

                    using (var sr = new StreamReader(ms))
                    {


                    }
                }
                if (sw != null)
                {
                    StringBuilder sb = new StringBuilder();

                    sb.Append(DateTime.Now.ToString("HH:mm:ss.fff")).Append(delmiter);
                    sb.Append(data.GetValue(1).ToString()).Append(delmiter);
                    sb.Append(Environment.NewLine);

                    sw.Write(sb.ToString());

                }
            });
        }

        private void CheckBoxUDP_Checked(object sender, RoutedEventArgs e)
        {
            textBlock1.Text = "open UDP settion";
            sendAddress = IPAddress.Parse(UDP_IPAddress.Text);
        }

        private void CheckBoxUDP_Unchecked(object sender, RoutedEventArgs e)
        {
            textBlock1.Text = "close UDP settion";

        }

        private void setPath_Click(object sender, RoutedEventArgs e)
        {
            // ファイル保存ダイアログ
            SaveFileDialog dlg = new SaveFileDialog();

            // デフォルトファイル名
            //            dlg.FileName = DateTime.Now.ToString("yyyyMMddHHmmssfff");
            dlg.FileName = "oh1_D" + DateTime.Now.ToString("yyyyMMdd") + "T" + DateTime.Now.ToString("HHmm");

            // デフォルトディレクトリ
            dlg.InitialDirectory = System.Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

            // ファイルのフィルタ
            dlg.Filter = "CSVファイル|*.csv|すべてのファイル|*.*";

            // ファイルの種類
            dlg.FilterIndex = 0;

            // 指定されたファイル名を取得

            if (dlg.ShowDialog() == true)
            {
                StringBuilder sb = new StringBuilder();

                sb.Append("Time").Append(delmiter);
                sb.Append("HR").Append(delmiter);
                sb.Append(Environment.NewLine);

                st = dlg.OpenFile();
                sw = new StreamWriter(st, Encoding.GetEncoding("UTF-8"));

                sw.Write(sb.ToString());
            }
            else
            {
            }
        }

        private void windowClosed(object sender, EventArgs e)
        {
            if (sw != null)
            {
                sw.Close();
            }
            if (st != null)
            {
                st.Close();
            }
        }
    }
}
