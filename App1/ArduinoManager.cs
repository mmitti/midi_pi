using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Spi;

namespace App1
{
    public class ArduinoManager : IDisposable
    {
        SpiDevice mDevice;
        public ArduinoManager()
        {
            
        }


        public void Dispose()
        {

            byte[] read = new byte[2];
            byte[] write = new byte[2] { 0, 0 };
            mDevice.TransferFullDuplex(write, read);
            mDevice.Dispose();
            mDevice = null;
        }

        public async Task Init()
        {
            var settings = new SpiConnectionSettings(0);
            settings.ClockFrequency = 50000;
            settings.Mode = SpiMode.Mode0;
            string spiAqs = SpiDevice.GetDeviceSelector("SPI0");
            var deviceInfo = await DeviceInformation.FindAllAsync(spiAqs);
            mDevice = await SpiDevice.FromIdAsync(deviceInfo[0].Id, settings);

            Suspend();
        }


        public void SendBarBeat(uint bar, byte beat)
        {
            //MMAA_AAAA  AAAA_0BBB MM=10 A:bar B:beat
            byte[] read = new byte[2];
            byte[] write = new byte[2];
            write[0] = (byte)((bar >> 4) & 0x3F | 0x80);
            write[1] = (byte)((((sbyte)bar & 0x0F) << 4) | beat & 0x07);
            mDevice.TransferFullDuplex(write, read);
            
        }

        public void SendTempo(uint tempo)
        {
            //MM00_00AA  AAAA_AAAA MM=11 A:tempo
            byte[] read = new byte[2];
            byte[] write = new byte[2];
            write[0] = (byte)(((tempo >> 8) & 0x03) | 0xC0);
            write[1] = (byte)(tempo & 0xFF);

            mDevice.TransferFullDuplex(write, read);

        }

        public byte ReadVol()
        {
            //MM00_00AA  AAAA_AAAA MM=11 A:tempo
            byte[] read = new byte[2];
            byte[] write = new byte[2];
            write[0] = (byte)(0x40);

            mDevice.TransferFullDuplex(write, read);
            return read[1];
        }

        internal void Suspend()
        {
            byte[] read = new byte[2];
            byte[] write = new byte[2] { 0, 0 };
            mDevice.TransferFullDuplex(write, read);
        }
    }
}
