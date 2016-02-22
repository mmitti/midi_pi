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
using Windows.Devices.Midi;
using Windows.Devices.Enumeration;
using MIDI;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.System;
using Windows.Devices.Gpio;
using System.Threading;

// 空白ページのアイテム テンプレートについては、http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 を参照してください

namespace App1
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private byte Code;
        byte Current;
        byte CH;
        IMidiOutPort MIDIPort;
        SMFPlayer mPlayer;
        ArduinoManager mArduino;
        GPIOManager mGPIO;
        int mTempoCount = 0;
        int[] mGameNoteIndexs;
        MidiGameNote[] mCurrentGameNote;
        SMF mSMF;
        DispatcherTimer mVolTimer;
        bool mIsInited;
        bool mIsPlaying;
        public  MainPage()
        {
            this.InitializeComponent();
            App.Current.Suspending += Current_Suspending;
            mArduino = new ArduinoManager();
            mGPIO = new GPIOManager();
            mGameNoteIndexs = new int[5] { 0,0,0,0,0 };
            mCurrentGameNote = new MidiGameNote[5];
            mIsInited = false;
            mIsPlaying = false;


            Task.Run(async () =>
            {
                SMFReader r = new SMFReader();
                mSMF = await r.Read(@"Assets\out.mid");
                var w = new MidiDeviceWatcher(MidiOutPort.GetDeviceSelector(), Dispatcher);
                w.Start();
                var col = w.GetDeviceInformationCollection();
                var l = await DeviceInformation.FindAllAsync(MidiOutPort.GetDeviceSelector());
                MIDIPort = await MidiOutPort.FromIdAsync(l[0].Id);
                await mArduino.Init();
                await mGPIO.Init(GpioController.GetDefault());
                mGPIO.MidiButtonChanged += MGPIO_MidiButtonChanged;
                mGPIO.JoyButtonChanged += MGPIO_JoyButtonChanged;
                mPlayer = new SMFPlayer(mSMF, MIDIPort);
                mPlayer.OnLED += Player_OnLED;
                mPlayer.OnBarBeatChanged += Player_BarBeatChanged;
                mPlayer.OnTempoChanged += Player_OnTempoChanged;
                mGPIO.Ack();
                mIsInited = true;

            });
            mVolTimer = new DispatcherTimer();
            mVolTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            mVolTimer.Tick += MVolTimer_Tick;
            mVolTimer.Start();
        }

        private void MGPIO_JoyButtonChanged(JoyDirection dir, bool push)
        {
            if (push) { 
                switch (dir)
                {
                    case JoyDirection.CENTER:
                        if (mIsPlaying) {
                            mArduino.Suspend();
                            App.Current.Exit();
                        }
                        else
                        {
                            mGPIO.ResetAckFail();
                            mPlayer.Play();
                            mIsPlaying = true;
                        }
                        break;
                }
            }
        }

        private void MVolTimer_Tick(object sender, object e)
        {
            if (!mIsInited) return;
            byte vol = mArduino.ReadVol();
            vol = (byte)(vol / 2);

            MIDIPort.SendMessage(new MidiControlChangeMessage(0, 11, vol));
        }

        private void Player_OnLED(byte id)
        {
            mGPIO.LineLEDOn(id);
        }

        private void MGPIO_MidiButtonChanged(int num, bool push)
        {
            var tick = mPlayer.TotalTick;
            if (push)
            {
                while (mGameNoteIndexs[num - 1] < mSMF.GameNotes[num].Count &&
                    mSMF.GameNotes[num][mGameNoteIndexs[num - 1]].EndTick < tick) mGameNoteIndexs[num - 1]++;
                if (mGameNoteIndexs[num - 1] >= mSMF.GameNotes[num].Count) return;
                if (mSMF.GameNotes[num][mGameNoteIndexs[num - 1]].Tick - 240 < tick && tick <  mSMF.GameNotes[num][mGameNoteIndexs[num - 1]].Tick + 240 ) mGPIO.Ack();
                else mGPIO.Fail();
                MIDIPort.SendMessage(new MidiNoteOnMessage(0, mSMF.GameNotes[num][mGameNoteIndexs[num - 1]].Note, 0x7F));
                mCurrentGameNote[num - 1] = mSMF.GameNotes[num][mGameNoteIndexs[num - 1]];
            }
            else
            {
                if(mCurrentGameNote[num - 1] != null)
                {
                    MIDIPort.SendMessage(new MidiNoteOffMessage(0, mCurrentGameNote[num - 1].Note, 0x64));
                }
                mCurrentGameNote[num - 1] = null;
            }
        }

        private void Current_Suspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            mArduino.Suspend();
        }

        private void Player_OnTempoChanged(uint tempo)
        {
            mArduino.SendTempo(tempo);
            mTempoCount = 2;
        }

        private void Player_BarBeatChanged(uint bar, byte beat)
        {
            if (mTempoCount > 0) mTempoCount--;
            else mArduino.SendBarBeat(bar, beat);
        }

      /*  private void Window_KeyUp(CoreWindow sender, KeyEventArgs args)
        {
            byte tmp = Code;
            Code &= (byte)(~KeyToI(args.VirtualKey));
            if (CH == 9)
            {
              
            }
            else
            {
                
                if (tmp != Code)
                {

                    if (Code != 0)
                    {
                        var midiMessageToSend = new MidiNoteOnMessage(CH, (byte)(Current + Code), 127);
                        MIDIPort.SendMessage(midiMessageToSend);
                    }
                    var midiOff = new MidiNoteOffMessage(CH, (byte)(Current + tmp), 0);
                    MIDIPort.SendMessage(midiOff);
                }
            }
        }

        private void Window_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            byte tmp = Code;
            Code |= KeyToI(args.VirtualKey);
            if (CH == 9) {
                byte diff = (byte)(tmp ^ Code);
                if((diff & 16) == 16) SendDram(35);
                if ((diff & 8) == 8) SendDram(40);
                if ((diff & 4) == 4) SendDram(38);
                if((diff & 3) != 0)
                {
                    switch(Code & 3)
                    {
                        case 1: SendDram(42); break;
                        case 2: SendDram(46); break;
                        case 3: SendDram(49); break;
                    }
                }
            }
            else
            {
                
                if (tmp != Code)
                {

                    if (Code != 0)
                    {
                        var midiMessageToSend = new MidiNoteOnMessage(CH, (byte)(Current + Code), 127);
                        MIDIPort.SendMessage(midiMessageToSend);
                    }
                    var midiOff = new MidiNoteOffMessage(CH, (byte)(Current + tmp), 0);
                    MIDIPort.SendMessage(midiOff);
                }
            }
        }*/

        private void SendDram(byte note)
        {
            var midiOn = new MidiNoteOnMessage(CH, note, 127);
            MIDIPort.SendMessage(midiOn);
            /*var midiOff = new MidiNoteOffMessage(CH, note, 127);
            MIDIPort.SendMessage(midiOff);*/
        }

/*
        private byte KeyToI(VirtualKey vk)
        {
            switch (vk)
            {
                case VirtualKey.F:return 1;
                case VirtualKey.D: return 2;
                case VirtualKey.S: return 4;
                case VirtualKey.A: return 8;
                case VirtualKey.Q: return 16;
            }
            return 0;
        }

        private async void button_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            
        }*/

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            mArduino.Dispose();
        }
    }
}
