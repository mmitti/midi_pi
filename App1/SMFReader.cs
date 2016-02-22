using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Midi;

namespace App1
{
    public class SMFReader
    {
        private byte[] SMFByte;
        public async Task<SMF> Read(String path) {
            SMF smf = new SMF();
            ulong cursol = 0;
            var sf = await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFileAsync(path);
            using (Stream fs = await sf.OpenStreamForReadAsync())
            {
                SMFByte = new byte[fs.Length];
                fs.Read(SMFByte, 0, SMFByte.Length);
            }

            byte[] head = ReadAsBytes(cursol, 4); cursol += 4;
            ulong head_len = ReadAsInt64(cursol); cursol += 4;
            smf.FormatType = ReadAsInt(cursol);   cursol += 2;
            smf.TrackNum = ReadAsInt(cursol);     cursol += 2;
            smf.TimeMode = ReadAsInt(cursol); cursol += 2;

            for(int i = 0; i < smf.TrackNum; i++)
            {
                byte[] track = ReadAsBytes(cursol, 4); cursol += 4;
                ulong track_len = ReadAsInt64(cursol); cursol += 4;
                ulong time = 0;
                for(ulong c = cursol; c < track_len + cursol;)
                {
                    var dt = ReadVlen(c);    c += dt.Item2;
                    time += dt.Item1;
                    //小節へ変換
                    byte dat = SMFByte[c];
                    if ((dat & 0xF0) == 0xF0)
                    {
                        byte type = SMFByte[c + 1]; c+=2;
                        var len = ReadVlen(c); c += len.Item2;
                        byte[] exdat = ReadAsBytes(c, len.Item1); c += len.Item1;
                        ulong beat = time / smf.TimeMode;
                        uint tick = (uint)(time % smf.TimeMode);
                        switch (type)
                        {
                            case 0x51:
                                smf.SystemEvents.Add(new TempoEvent(beat, tick, exdat[0], exdat[1], exdat[2]));
                                break;
                            case 0x58:
                                smf.SystemEvents.Add(new MeterEvent(beat, tick, exdat[0], exdat[1], exdat[2], exdat[3]));
                                break;
                            case 0x59:
                                smf.SystemEvents.Add(new KeyEvent(beat, tick, exdat[0], exdat[1]));
                                break;
                        }
                        /*

                        FF type len dats
                        FF 01-05 len text
                        FF 2F 00 	End of Track
                        FF 51 03 tempo(3byte) 	Set Tempo 	テンポ。 4分音符の長さをマイクロ秒単位で表現。
                        FF 58 04 nn dd cc bb 	Time Signature 	拍子nn=分子 dd=分子（2の負のべき乗で表す） cc=メトロノーム間隔（四分音符間隔なら18H） bb=四分音符あたりの三十二分音符の数
                        FFH 59H 02H sf ml 	Key Signature 	キー（調）を表す sf=正・・シャープの数 負・・フラットの数 ml=0・・・長調 1・・・短調 
                        */
                    }
                    else {
                        byte ch = (byte)(dat & 0x0F);
                        switch(dat & 0xF0)
                        {
                            case 0x80:
                                if (ch == 0 && SMFByte[c + 2] <= 5 && smf.GameNotes[SMFByte[c + 2]].Count > 0)
                                {
                                    foreach(var mg in smf.GameNotes[SMFByte[c + 2]])
                                    {
                                        if(mg.Note == SMFByte[c + 1] && !mg.IsSetEnd)
                                        {
                                            mg.IsSetEnd = true;
                                            mg.EndTick = time;
                                            break;
                                        }
                                    }
                                }
                                else {
                                    smf.CHData[ch].Add(new NoteOFF(ch, time, SMFByte[c + 1], SMFByte[c + 2]));
                                }
                                c += 3;
                                break;
                            case 0x90:
                                if (ch == 0 && SMFByte[c + 2] <= 5)
                                {
                                    smf.GameNotes[SMFByte[c + 2]].Add(
                                        new MidiGameNote(time, SMFByte[c + 1]));
                                }
                                else {
                                    smf.CHData[ch].Add(new NoteON(ch, time, SMFByte[c + 1], SMFByte[c + 2]));
                                }
                                c += 3;
                                break;
                            case 0xB0:
                                if (SMFByte[c + 1] == 12)
                                {
                                    smf.SystemEvents.Add(new LEDEvent(time / smf.TimeMode, (uint)(time % smf.TimeMode), SMFByte[c + 2]));
                                }
                                else {
                                    smf.CHData[ch].Add(new ControleChange(ch, time, SMFByte[c + 1], SMFByte[c + 2]));
                                }
                                c += 3;
                                break;
                            case 0xC0:
                                smf.CHData[ch].Add(new ProgramChange(ch, time, SMFByte[c + 1]));
                                c += 2;
                                break;
                            case 0xE0:
                                smf.CHData[ch].Add(new PitchBend(ch, time, SMFByte[c + 1], SMFByte[c + 2]));
                                c += 3;
                                break;
                        }
                    }

                }
                cursol += track_len;
            }
            smf.InitMidiMessage();
            return smf;

        }
        private byte[] ReadAsBytes(ulong cursol, uint size)
        {
            byte[] r = new byte[size];
            for (uint i = 0; i < size; i++)
            {
                r[i] = SMFByte[cursol + i];
            }
            return r;
        }
        private UInt64 ReadAsInt64(ulong cursol){
            UInt64 r = 0;
            for(uint i = 0; i < 4; i++)
            {
                r += (ulong)SMFByte[cursol + i] << (int)(8 * (3 - i));
            }
            return r;
        }

        private UInt32 ReadAsInt(ulong cursol)
        {
            UInt32 r = 0;
            for (uint i = 0; i < 2; i++)
            {
                r += (uint)SMFByte[cursol + i] << (int)(8 * (1 - i));
            }
            return r;
        }

        private Tuple<uint, uint> ReadVlen(ulong cursol)
        {
            uint size = 0;
            uint time = 0;
            do
            {
                size++;
                time = time << 7;
                time |= (uint)(SMFByte[cursol + size-1] & 0x7F);
            } while (size < 4 && (SMFByte[cursol + size-1] & 0x80) == 0x80);

            return new Tuple<uint, uint>(time, size);
        }

       
    }

    public class SMF
    {
        public uint FormatType;
        public uint TrackNum;
        public uint TimeMode;
        public List<List<MIDIEvent>> CHData;
        public List<SysEvent> SystemEvents;
        public List<MidiMessageContainer> MidiMessages;
        public Dictionary<int, List<MidiGameNote>> GameNotes;
        public SMF()
        {
            CHData = new List<List<MIDIEvent>>();
            for(int i= 0; i < 16; i++)
            {
                CHData.Add(new List<MIDIEvent>());
            }
            SystemEvents = new List<SysEvent>();
            MidiMessages = new List<MidiMessageContainer>();
            GameNotes = new Dictionary<int, List<MidiGameNote>>();
            for(int i = 1; i <= 5; i++)
            {
                GameNotes[i] = new List<MidiGameNote>();
            }
        }
        public void InitMidiMessage(){
            foreach(var l in CHData)
            {
                foreach (var i in l)
                {
                    MidiMessageContainer c = new MidiMessageContainer();
                    c.Beat = i.Time / TimeMode;
                    c.Tick = (uint)(i.Time % TimeMode);
                    c.Message = i.ToMidiMessage();
                    MidiMessages.Add(c);
                }
            }
            MidiMessages.Sort();
        }
    }

    public class MidiGameNote : IComparable<MidiGameNote>
    {
        public ulong Tick;
        public ulong EndTick;
        public byte Note;
        public bool IsSetEnd = false;

        public MidiGameNote(ulong tick, byte note)
        {
            Tick = tick;
            Note = note;
        }

        public int CompareTo(MidiGameNote other)
        {
             return (int)Tick - (int)other.Tick;
        }
    }

    public struct MidiMessageContainer : IComparable<MidiMessageContainer>
    {
        public ulong Beat;
        public uint Tick;
        public IMidiMessage Message;

        public int CompareTo(MidiMessageContainer other)
        {
            if(Beat == other.Beat)
            {
                return (int)Tick - (int)other.Tick;
            }
            return (int)Beat - (int)other.Beat;
        }
    }

    public interface SysEvent
    {
        ulong Beat { get; }
        uint Tick { get; }
    }
    /*	Set Tempo 	テンポ。 4分音符の長さをマイクロ秒単位で表現。
Meter                        FF 58 04 nn dd cc bb 	Time Signature 	拍子nn=分子 dd=分子（2の負のべき乗で表す） cc=メトロノーム間隔（四分音符間隔なら18H） bb=四分音符あたりの三十二分音符の数
                        FFH 59H 02H sf ml 	Key Signature */

    public interface Tempo
    {
        uint TempoUS { get; }
    }
    public class TempoEvent : SysEvent, Tempo
    {

        public ulong Beat { get; }
        public uint Tick { get; }
        uint ms;
        public TempoEvent(ulong beat,uint tick, byte b1, byte b2, byte b3)
        {
            Beat = beat;
            Tick = tick;
            ms = (uint)(b1<<16|b2<<8|b3);
        }

        public uint TempoUS
        {
            get
            {
                return ms;
            }
        }

    }

    public interface Meter
    {
        /// <summary>
        /// 拍子
        /// </summary>
        byte Meter { get; }
        /// <summary>
        /// 分子
        /// </summary>
        byte MeterLength { get; }
        /// <summary>
        /// メトロノーム間隔/24で4分音符長
        /// </summary>
        byte Metrnome { get; }
        /// <summary>
        /// 32分音符数/4分音符　通常8
        /// </summary>
        byte NoteCount32 { get; }
    }

    public class MeterEvent : SysEvent, Meter
    {

        public byte Meter { get; }

        public byte MeterLength { get; }

        public byte Metrnome { get; }

        public byte NoteCount32 { get; }
        public ulong Beat { get; }
        public uint Tick { get; }

        public MeterEvent(ulong beat,uint tick, byte met, byte len, byte metrnome, byte notec_32)
        {
            Beat = beat;
            Tick = tick;
            Meter = met;
            MeterLength = (byte)(2<<len);
            Metrnome = metrnome;
            NoteCount32 = notec_32;
        }

    }


    public interface Key
    {
        /// <summary>
        /// Key 正シャープの数
        /// </summary>
        sbyte Key { get; }
        /// <summary>
        /// Tなら短調
        /// </summary>
        bool KeyType { get; }
    }
    public class KeyEvent : SysEvent, Key
    {

        public ulong Beat { get; }
        public uint Tick { get; }
        /// <summary>
        /// Key 正シャープの数
        /// </summary>
        public sbyte Key { get; }
        /// <summary>
        /// Tなら短調
        /// </summary>
        public bool KeyType { get; }

        public KeyEvent(ulong beat, uint tick, byte k, byte t)
        {
            Beat = beat;
            Tick = tick;
            Key = (sbyte)k;
            KeyType = t == 0x1;
        }

    }

    public class LEDEvent : SysEvent
    {

        public ulong Beat { get; }
        public uint Tick { get; }
        byte mData;
        public LEDEvent(ulong beat, uint tick, byte d)
        {
            Beat = beat;
            Tick = tick;
            mData = d;
        }

        public byte LED
        {
            get
            {
                return mData;
            }
        }

    }

    public interface MIDIEvent
    {
        byte CH { get;}
        ulong Time { get; }
        IMidiMessage ToMidiMessage();
    }

    public class NoteON : MIDIEvent
    {
        public NoteON(byte ch, ulong time, byte note, byte vel)
        {
            CH = ch;
            Time = time;
            NoteNum = note;
            Velocity = vel;
        }
        byte CH;
        ulong Time;

        byte MIDIEvent.CH
        {
            get
            {
                return CH;
            }
        }
        ulong MIDIEvent.Time
        {
            get
            {
                return Time;
            }
        }
        byte NoteNum;
        byte Velocity;
        public IMidiMessage ToMidiMessage()
        {
            return new MidiNoteOnMessage(CH, NoteNum, Velocity);
        }
    }

    public class NoteOFF : MIDIEvent
    {
        public NoteOFF(byte ch, ulong time, byte note, byte vel)
        {
            CH = ch;
            Time = time;
            NoteNum = note;
            Velocity = vel;
        }

        byte CH;
        ulong Time;

        byte MIDIEvent.CH
        {
            get
            {
                return CH;
            }
        }
        ulong MIDIEvent.Time
        {
            get
            {
                return Time;
            }
        }
        byte NoteNum;
        byte Velocity;
        public IMidiMessage ToMidiMessage()
        {
            return new MidiNoteOffMessage(CH, NoteNum, Velocity);
        }
    }

    public class ProgramChange : MIDIEvent
    {
        public ProgramChange(byte ch, ulong time, byte p)
        {
            CH = ch;
            Time = time;
            ProgramNum = p;
        }

        byte CH;
        ulong Time;

        byte MIDIEvent.CH
        {
            get
            {
                return CH;
            }
        }
        ulong MIDIEvent.Time
        {
            get
            {
                return Time;
            }
        }
        byte ProgramNum;
        public IMidiMessage ToMidiMessage()
        {
            return new MidiProgramChangeMessage(CH, ProgramNum);
        }
    }

    public class PitchBend : MIDIEvent
    {
        public PitchBend(byte ch, ulong time, byte lsb, byte msb)
        {
            CH = ch;
            Time = time;
            Pitch = (ushort)((msb << 7) | lsb & 0x7F);
        }

        byte CH;
        ulong Time;

        byte MIDIEvent.CH
        {
            get
            {
                return CH;
            }
        }
        ulong MIDIEvent.Time
        {
            get
            {
                return Time;
            }
        }
        ushort Pitch;
        public IMidiMessage ToMidiMessage()
        {
            return new MidiPitchBendChangeMessage(CH, Pitch);
        }
    }

    public class ControleChange: MIDIEvent
    {
        public ControleChange(byte ch, ulong time, byte cc, byte dt)
        {
            CH = ch;
            Time = time;
            ControleNum = cc;
            Data = dt;
        }
        byte CH;
        ulong Time;

        byte MIDIEvent.CH
        {
            get
            {
                return CH;
            }
        }
        ulong MIDIEvent.Time
        {
            get
            {
                return Time;
            }
        }
        byte ControleNum;
        byte Data;
        public IMidiMessage ToMidiMessage()
        {
            return new MidiControlChangeMessage(CH, ControleNum, Data);
        }
    }


}
