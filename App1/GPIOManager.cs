using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Gpio;

namespace App1
{
    public enum JoyDirection
    {
        CENTER, NA
    }
    public delegate void MidiButtonChangedEvent(int num, bool push);
    public delegate void JoyButtonChangedEvent(JoyDirection dir, bool push);
    public class GPIOManager
    {
        GpioPin mButtonA;
        GpioPin mButtonB;
        GpioPin mButtonC;
        GpioPin mButtonD;
        GpioPin mButtonE;
        GpioPin mAckPin;
        GpioPin mFailPin;
        GpioPin mCenterPin;
        Dictionary<int, GpioPin> mLineLEDPin;
        public event MidiButtonChangedEvent MidiButtonChanged;
        public event JoyButtonChangedEvent JoyButtonChanged;
        public GPIOManager()
        {

        }

        private GpioPin open(int port)
        {
            GpioController c = GpioController.GetDefault();
            GpioPin pin = c.OpenPin(12);
            pin.SetDriveMode(GpioPinDriveMode.Output);
            pin.Write(GpioPinValue.High);

            return pin;
        }

        private void PinValueChanged(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            throw new NotImplementedException();
        }

        private GpioPin InitBtnPin(int port, GpioController ctrl)
        {
            var p = ctrl.OpenPin(port);
            p.SetDriveMode(GpioPinDriveMode.InputPullUp);
            p.DebounceTimeout = new TimeSpan(200);
            return p;
        }
        private GpioPin InitLEDPin(int port, GpioController ctrl)
        {
            var p = ctrl.OpenPin(port);
            p.SetDriveMode(GpioPinDriveMode.Output);
            p.Write(GpioPinValue.Low);
            return p;
        }

        public async Task Init(GpioController gpio) 
        {
            mButtonA = InitBtnPin(5, gpio);
            mButtonB = InitBtnPin(6, gpio);
            mButtonC = InitBtnPin(13, gpio);
            mButtonD = InitBtnPin(19, gpio);
            mButtonE = InitBtnPin(26, gpio);
            mAckPin = InitLEDPin(23, gpio);
            mFailPin = InitLEDPin(24, gpio);
            mButtonA.ValueChanged += MidiButtonValueChanged;
            mButtonB.ValueChanged += MidiButtonValueChanged;
            mButtonC.ValueChanged += MidiButtonValueChanged;
            mButtonD.ValueChanged += MidiButtonValueChanged;
            mButtonE.ValueChanged += MidiButtonValueChanged;
            mLineLEDPin = new Dictionary<int, GpioPin>();
            mLineLEDPin.Add(4, InitLEDPin(4, gpio));
            mLineLEDPin.Add(5, InitLEDPin(17, gpio));
            mLineLEDPin.Add(6, InitLEDPin(27, gpio));
            mLineLEDPin.Add(7, InitLEDPin(18, gpio));
            mCenterPin = InitBtnPin(12, gpio);
            mCenterPin.ValueChanged += MCenterPin_ValueChanged;
        }

        private void MCenterPin_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            JoyDirection dir = JoyDirection.NA;
            if (sender == mCenterPin) dir = JoyDirection.CENTER;
            if (JoyButtonChanged != null) JoyButtonChanged(JoyDirection.CENTER, args.Edge == GpioPinEdge.FallingEdge);
        }

        public void Ack()
        {
            mAckPin.Write(GpioPinValue.High);
            mFailPin.Write(GpioPinValue.Low);
        }
        public void Fail()
        {
            mAckPin.Write(GpioPinValue.Low);
            mFailPin.Write(GpioPinValue.High);
        }
        public void ResetAckFail()
        {

            mAckPin.Write(GpioPinValue.Low);
            mFailPin.Write(GpioPinValue.Low);
        }

        private void MidiButtonValueChanged(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            int num = 0;
            if (sender == mButtonA) num = 1;
            else if (sender == mButtonB) num = 2;
            else if (sender == mButtonC) num = 3;
            else if (sender == mButtonD) num = 4;
            else if (sender == mButtonE) num = 5;
            if (MidiButtonChanged != null) MidiButtonChanged(num, args.Edge == GpioPinEdge.FallingEdge);
        }
        public void LineLEDOn(byte id)
        {
            foreach(var k in mLineLEDPin)
            {
                if (k.Key == id) k.Value.Write(GpioPinValue.High);
                else k.Value.Write(GpioPinValue.Low);
            }
        }
    }
}
