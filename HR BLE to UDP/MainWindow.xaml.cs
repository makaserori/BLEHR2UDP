﻿using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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

            this.GattDeviceService = await GattDeviceService.FromIdAsync(deviceInformation.Id);
            MessageBox.Show($"found {deviceInformation.Id}");
        }

        private async void ButtonReadValue_Click(object sender, RoutedEventArgs e)
        {
            if (this.GattDeviceService == null)
            {
                MessageBox.Show("Please click connect button");
                return;
            }

            // 値を読み始める
            if (this.GattCharacteristic == null)
            {
                this.GattCharacteristic = this.GattDeviceService.GetCharacteristics(new Guid(SensorTagUuid.UuidIrtData)).First();
                this.GattCharacteristic.ValueChanged += this.GattCharacteristic_ValueChanged;

                var status = await this.GattCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                if (status == GattCommunicationStatus.Unreachable)
                {
                    MessageBox.Show("Failed");
                }

            }
        }

        private async void GattCharacteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            // 値を読んで表示する
            await this.Dispatcher.InvokeAsync(() =>
            {
                var data = new byte[args.CharacteristicValue.Length];
                DataReader.FromBuffer(args.CharacteristicValue).ReadBytes(data);
                this.textBlock1.Text = "Sample No. " + count.ToString() + "\n HR: " + data.GetValue(1).ToString();
                count++;

                if (this.checkBoxUDP.IsChecked == true)
                {
                    using (OscSender oscSender = new OscSender(sendAddress, Int32.Parse(this.UDP_Port.Text)))
                    {
                        // 接続
                        oscSender.Connect();

                        // OSC送信
                        oscSender.Send(new OscMessage("/oh1", data.GetValue(1).ToString()));
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
            this.textBlock1.Text = "open UDP settion";
            sendAddress = IPAddress.Parse(this.UDP_IPAddress.Text);
        }

        private void CheckBoxUDP_Unchecked(object sender, RoutedEventArgs e)
        {
            this.textBlock1.Text = "close UDP settion";

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
