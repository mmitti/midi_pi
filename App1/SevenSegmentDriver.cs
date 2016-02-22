using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Gpio;

namespace App1
{
    public class SevenSegmentDriver
    {
        GpioPin CommonNum1;
        GpioPin CommonNum2;
        GpioPin CommonNum3;
        GpioPin CommonNum4;
        GpioPin DispA;
        GpioPin DispB;
        GpioPin DispC;
        GpioPin DispD;
        GpioPin DispE;
        GpioPin DispF;
        GpioPin DispG;
        GpioPin DotPoint;
        private GpioPin InitPin(int port, GpioController ctrl)
        {
            var p = ctrl.OpenPin(port);
            p.SetDriveMode(GpioPinDriveMode.Output);
            p.Write(GpioPinValue.Low);
            return p;
        }
        public SevenSegmentDriver(GpioController ctrl, SevenSegmentConfig cfg)
        {

            CommonNum1 = InitPin(cfg.CommonNum1Pin, ctrl);
            CommonNum2 = InitPin(cfg.CommonNum2Pin, ctrl);
            CommonNum3 = InitPin(cfg.CommonNum3Pin, ctrl);
            CommonNum4 = InitPin(cfg.CommonNum4Pin, ctrl);

            DispA = InitPin(cfg.DispAPin, ctrl);
            DispB = InitPin(cfg.DispBPin, ctrl);
            DispC = InitPin(cfg.DispCPin, ctrl);
            DispD = InitPin(cfg.DispDPin, ctrl);
            DispE = InitPin(cfg.DispEPin, ctrl);
            DispF = InitPin(cfg.DispFPin, ctrl);
            DispG = InitPin(cfg.DispGPin, ctrl);
            DotPoint = InitPin(cfg.DotPointPin, ctrl);
        }

        public void WeirwNum(uint n)
        {
            byte[] d = new byte[4];
            for(int i = 0; i < 4; i++)
            {
                d[i] = (byte)(n % 10);
                n = n / 10;
            }
            Write(d[3], d[2], d[1], d[0]);
        }
        public void WeireBarBeat(uint bar, byte beat)
        {
            byte[] d = new byte[3];
            for (int i = 0; i < 3; i++)
            {
                d[i] = (byte)(bar % 10);
                bar = bar / 10;
            }
            Write(d[2], d[1], (byte)(d[0] | 0x80), beat);
        }

        //0bABBB_BBBB  A:DP, B:0~9, 0xA~0xF
        public void Write(byte d1, byte d2, byte d3, byte d4)
        {
            CommonNum1.Write(GpioPinValue.High);
            CommonNum2.Write(GpioPinValue.High);
            CommonNum3.Write(GpioPinValue.High);
            CommonNum4.Write(GpioPinValue.High);
            Write(CommonNum1, d1);
            Write(CommonNum2, d2);
            Write(CommonNum3, d3);
            Write(CommonNum4, d4);
        }

        private void Write(GpioPin commonpin, byte d)
        {
            bool dp = (d & 0x80) == 0x80;
            byte num = (byte)(d & 0x7F);
            switch (num)
            {
                case 0:
                    Write(commonpin, a: true, b: true, c: true, d: true, e: true, f: true, dp: dp);
                    break;
                case 1:
                    Write(commonpin, b: true, c: true, dp: dp);
                    break;
                case 2:
                    Write(commonpin, a: true, b: true, g: true, e: true, d: true, dp: dp);
                    break;
                case 3:
                    Write(commonpin, a: true, b: true, g: true, c: true, d: true, dp: dp);
                    break;
                case 4:
                    Write(commonpin, f: true, g: true, b: true, c: true, dp: dp);
                    break;
                case 5:
                    Write(commonpin, a: true, f: true, g: true, c: true, d: true, dp: dp);
                    break;
                case 6:
                    Write(commonpin, a: true, f: true, e: true, d: true, c: true, g: true, dp: dp);
                    break;
                case 7:
                    Write(commonpin, a: true, b: true, c: true, dp: dp);
                    break;
                case 8:
                    Write(commonpin, a: true, b: true, c: true, d: true, e: true, f: true, g: true, dp: dp);
                    break;
                case 9:
                    Write(commonpin, a: true, b: true, c: true, d: true, f: true, g: true, dp: dp);
                    break;
                case 0x0A:
                    Write(commonpin, a: true, b: true, c: true, e: true, f: true, g: true, dp: dp);
                    break;
                case 0x0B:
                    Write(commonpin, c: true, d: true, e: true, f: true, g: true, dp: dp);
                    break;
                case 0x0C:
                    Write(commonpin, a: true, d: true, e: true, f: true, g: true, dp: dp);
                    break;
                case 0x0D:
                    Write(commonpin, b: true, d: true, c: true, e: true, g: true, dp: dp);
                    break;
                case 0x0E:
                    Write(commonpin, a: true, d: true, e: true, f: true, g: true, dp: dp);
                    break;
                case 0x0F:
                    Write(commonpin, a: true, e: true, f: true, g: true, dp: dp);
                    break;
                default:
                    Write(commonpin, dp: dp);
                    break;

            }
        }

        private void Write(GpioPin commonpin, bool a=false, bool b = false, bool c = false, bool d = false, bool e = false, bool f = false, bool g = false, bool dp = false)
        {
            WriteElement(commonpin, DispA, a);
            WriteElement(commonpin, DispB, b);
            WriteElement(commonpin, DispC, c);
            WriteElement(commonpin, DispD, d);
            WriteElement(commonpin, DispE, e);
            WriteElement(commonpin, DispF, f);
            WriteElement(commonpin, DispG, g);
            WriteElement(commonpin, DotPoint, dp);
        }

        private void WriteElement(GpioPin commonpin, GpioPin numpin, bool disp)
        {
            commonpin.Write(GpioPinValue.Low);
            numpin.Write(disp?GpioPinValue.High: GpioPinValue.Low);
            commonpin.Write(GpioPinValue.High);
            numpin.Write(GpioPinValue.Low);
        }
    }

    /* A
      F B
       G
      E C
       D  DP
    */
    public struct SevenSegmentConfig
    {
        public int CommonNum1Pin { get; }
        public int CommonNum2Pin { get; }
        public int CommonNum3Pin { get; }
        public int CommonNum4Pin { get; }
        public int DispAPin { get; }
        public int DispBPin { get; }
        public int DispCPin { get; }
        public int DispDPin { get; }
        public int DispEPin { get; }
        public int DispFPin { get; }
        public int DispGPin { get; }
        public int DotPointPin { get; }
        public SevenSegmentConfig(int c1, int c2, int c3, int c4, int a, int b, int c, int d, int e, int f, int g, int dp)
        {
            CommonNum1Pin = c1;
            CommonNum2Pin = c2;
            CommonNum3Pin = c3;
            CommonNum4Pin = c4;
            DispAPin = a;
            DispBPin = b;
            DispCPin = c;
            DispDPin = d;
            DispEPin = e;
            DispFPin = f;
            DispGPin = g;
            DotPointPin = dp;
        }
    }
}
