using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace ConsoleApp2
{
    class Program
    {
        static int time = 0;
        static int usec = 0;
        static int format = 0;
        static int tracks = 0;
        static int[] inst = Enumerable.Repeat(-1, 16).ToArray();
        static void Main(string[] args)
        {
            //ファイル名
            var midi = "_summer(pf+quartet)";
            //ファイルひらく
            var fileName = @"C:\Users\saku_\Desktop\DeskTap-master\sakurai\" + midi + ".mid";
            var br = new BinaryReader(new FileStream(fileName, FileMode.Open));
            fileName = @"C:\Users\saku_\Desktop\DeskTap-master\sakurai\" + midi + ".txt";
            var sw = new StreamWriter(fileName, false, Encoding.GetEncoding("SHIFT_JIS"));
            //ヘッダを処理(format,tracksの取得)
            GetHeader(br);

            //実データ部から情報取得
            var dataArray = new List<string[]>[tracks];
            for (int i = 0; i < tracks; i++) dataArray[i] = MakeDataList(br, sw);

            //テキスト出力
            for (int num = 0; num < tracks; num++)
            {
                var ch = Enumerable.Repeat(false, 16).ToArray();
                for (int i = 0; i < dataArray[num].Count(); i++) ch[int.Parse(dataArray[num][i][0])] = true;//使ってるチャンネルだけtrue

                for (int i = 0; i < 16; i++)
                {
                    if (ch[i])
                    {
                        sw.WriteLine("//channel:{0}", i + 1);
                        sw.WriteLine("//inst:{0}", inst[i] + 1);
                        WriteNoteLength(sw, dataArray[num], i);
                        WriteTimeAry(sw, dataArray[num], i);
                        WriteNoteAry(sw, dataArray[num], i);
                        WriteVolume(sw, dataArray[num], i);
                    }
                }
            }

            //ファイルとじる
            br.Close();
            sw.Close();
        }
        public static void GetHeader(BinaryReader br)
        {
            var getStr = new List<string>();
            MidiRead(br, 8);//開始部(4byte)＋ヘッダ長指定(4byte) => 無視
            getStr = MidiRead(br, 4);//フォーマットタイプ(4byte)＋トラック数(4byte)
            format = GetInt(getStr[1]);
            tracks = GetInt(getStr[3]);
            getStr = MidiRead(br, 2);//時間単位(2byte)
            time = 256 * GetInt(getStr[0]) + GetInt(getStr[1]);
            //MidiRead(br, 8);//トラック開始部(4byte)+実データ部長(4byte) => 無視
        }
        public static List<string[]> MakeDataList(BinaryReader br, StreamWriter sw)
        {
            MidiRead(br, 8);
            var notes = new string[12] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            bool EOF = false;
            var data = new List<string[]>();
            int DT = 0;
            string cash = "xx";

            while (!EOF)//トラックの最後まで処理
            {
                DT += GetDT(br);
                string next = MidiRead(br, 1)[0];
                string order = "xx";
                if (next[0] >= '8')
                {
                    cash = order = next;
                    if (order == "FF")//メタイベント
                    {
                        switch (MidiRead(br, 1)[0])//次のバイナリ(=メタイベント番号)により動作
                        {
                            case "2F"://トラック終端
                                EOF = true;
                                MidiRead(br, 1);
                                break;
                            case "51"://テンポ変更
                                GetUsec(br);
                                break;
                            default://その他（使わない=>不要分の情報をスキップ）
                                int skip = GetInt(MidiRead(br, 1)[0]);
                                MidiRead(br, skip);
                                break;
                        }
                    }
                    else if (order[0] == '9')//音再生　9* = *チャンネルで音を出す
                    {
                        var sound = MidiRead(br, 2);//(sound[0],sound[1])=(音階,音量)
                        if (GetInt(sound[1]) != 0)//音量ゼロでなければ
                        {
                            //data[0-3]=(チャンネル, DeltaTime, 音階, null(後で音符情報入れる,音量)
                            data.Add(new string[5] { GetInt(order[1].ToString()).ToString(),
                            DT.ToString(), notes[GetInt(sound[0]) % 12] + (GetInt(sound[0]) / 12 - 1).ToString(),
                            "0", sound[1]});
                            //DT = 0;※仕様変更
                        }
                        else//音量ゼロなら音符情報を加える
                        {
                            for (int i = data.Count() - 1; i >= 0; i--)
                            {
                                if (data[i][3] == "0" && data[i][2] == notes[GetInt(sound[0]) % 12] + (GetInt(sound[0]) / 12 - 1).ToString())//音符長さ情報なし　かつ　音階一致
                                {
                                    data[i][3] = DT.ToString();
                                    break;
                                }
                            }
                        }
                    }
                    else//8* ** **, A* ** **, B* ** **, C* **, D* **, E* ** **の処理
                    {
                        if (order[0] == 'C' || order[0] == 'D')
                        {
                            var sL = MidiRead(br, 1);
                            if (order[0] == 'C')
                            {
                                string temp = "0" + order[1].ToString();
                                inst[GetInt(temp)] = GetInt(sL[0]);
                            }
                        }
                        else MidiRead(br, 2);
                    }
                }
                else//ランニングステータスを使用
                {
                    Console.WriteLine("run{0}", cash);
                    order = cash;
                    if (order == "FF")//メタイベント
                    {
                        switch (next)//次のバイナリ(=メタイベント番号)により動作
                        {
                            case "2F"://トラック終端
                                EOF = true;
                                MidiRead(br, 1);
                                break;
                            case "51"://テンポ変更
                                GetUsec(br);
                                break;
                            default://その他（使わない=>不要分の情報をスキップ）
                                int skip = GetInt(MidiRead(br, 1)[0]);
                                MidiRead(br, skip);
                                break;
                        }
                    }
                    else if (order[0] == '9')//音再生　9* = *チャンネルで音を出す
                    {
                        var sound = MidiRead(br, 1);//(sound[0],sound[1])=(音階,音量)
                        if (GetInt(sound[0]) != 0)//音量ゼロでなければ
                        {
                            //data[0-2]=(チャンネル, DeltaTime, 音階)
                            data.Add(new string[5] { GetInt(order[1].ToString()).ToString(),
                            DT.ToString(), notes[GetInt(next) % 12] + (GetInt(next) / 12 - 1).ToString(),
                            "0",sound[0]});
                            //DT = 0;※仕様変更
                        }
                        else//音量ゼロなら音符情報を加える
                        {
                            for (int i = data.Count() - 1; i >= 0; i--)
                            {
                                if (data[i][3] == string.Empty && data[i][2] == "0")//音符長さ情報なし　かつ　音階一致
                                {
                                    data[i][3] = DT.ToString();
                                    break;
                                }
                            }
                        }
                    }
                    else//8* ** **, A* ** **, B* ** **, C* **, D* **, E* ** **の処理
                    {
                        if (order[0] == 'C' || order[0] == 'D')
                        {
                            var sL = MidiRead(br, 1);
                            if (order[0] == 'C')
                            {
                                inst[int.Parse(order[1].ToString())] = GetInt(sL[0]);
                            }
                        }
                        else MidiRead(br, 2);
                    }
                }
            }
            return data;
        }
        public static void WriteVolume(StreamWriter sw, List<string[]> data, int ch)
        {
            int count = 0;
            var list = new List<string>();
            sw.WriteLine("var melody_volume_ary" + (ch + 1) + " = [");
            for (int i = 0; i < data.Count(); i++)
            {
                if (data[i][0] == ch.ToString()) list.Add(data[i][4]);
            }
            for (int i = 0; i < list.Count(); i++)
            {
                sw.Write(GetInt(list[i]));
                if (i != list.Count() - 1) sw.Write(',');
                count++;
                if (count == 16)
                {
                    sw.WriteLine("");
                    count = 0;
                }
            }
            sw.WriteLine("\r\n];");
        }
        public static void WriteNoteLength(StreamWriter sw, List<string[]> data, int ch)
        {
            double start = 0;
            double end = 0;
            int count = 0;
            var list = new List<double>();
            sw.WriteLine("var melody_length_ary" + (ch + 1) + " = [");
            for (int i = 0; i < data.Count(); i++)
            {
                start = Math.Round(double.Parse(data[i][1]) / time * (usec / 1000));
                end = Math.Round(double.Parse(data[i][3]) / time * (usec / 1000));
                if (data[i][0] == ch.ToString()) list.Add((end - start) / time / 4);
            }
            for (int i = 0; i < list.Count(); i++)
            {
                sw.Write(list[i]);
                if (i != list.Count() - 1) sw.Write(',');
                count++;
                if (count == 16)
                {
                    sw.WriteLine("");
                    count = 0;
                }
            }
            sw.WriteLine("\r\n];");
        }
        public static void WriteTimeAry(StreamWriter sw, List<string[]> data, int ch)
        {
            int msec = 0;
            int count = 0;
            var list = new List<int>();
            sw.WriteLine("var melody_time_ary" + (ch + 1) + " = [");
            for (int j = 0; j < data.Count(); j++)
            {
                msec = (int)Math.Round(double.Parse(data[j][1]) / time * (usec / 1000));
                if (data[j][0] == ch.ToString()) list.Add(msec);
            }
            for (int i = 0; i < list.Count(); i++)
            {
                sw.Write(list[i]);
                if (i != list.Count() - 1) sw.Write(',');
                count++;
                if (count == 16)
                {
                    sw.WriteLine("");
                    count = 0;
                }
            }
            sw.WriteLine("\r\n];");
        }
        public static void WriteNoteAry(StreamWriter sw, List<string[]> data, int ch)
        {
            int count = 0;
            var list = new List<string>();
            sw.WriteLine("var melody_data" + (ch + 1) + " = [");
            for (int i = 0; i < data.Count(); i++) if (data[i][0] == ch.ToString()) list.Add(data[i][2]);
            for (int i = 0; i < list.Count(); i++)
            {
                sw.Write("'" + list[i] + "'");
                if (i != list.Count() - 1) sw.Write(",");
                count++;
                if (count == 16)
                {
                    sw.WriteLine("");
                    count = 0;
                }
            }

            sw.WriteLine("\r\n];");
        }
        public static List<string> MidiRead(BinaryReader br, int n)
        {
            var ret = new List<string>();
            var str = br.ReadBytes(n);
            for (int i = 0; i < n; i++)
            {
                ret.Add(str[i].ToString("X2"));
            }
            return ret;
        }
        public static int GetInt(string str)
        {
            return Convert.ToInt32(str, 16);
        }
        public static int GetDT(BinaryReader br)
        {
            int ret = 128;
            int add = 0;
            do
            {
                ret -= 128;
                ret *= 128;
                add = GetInt(MidiRead(br, 1)[0]);
                ret += add;
            } while (add >= 128);
            return ret;
        }
        public static void GetUsec(BinaryReader br)
        {
            usec = 0;
            int num = GetInt(MidiRead(br, 1)[0]);
            do
            {
                usec += GetInt(MidiRead(br, 1)[0]) * (int)Math.Pow(256, num - 1);
                num--;
            } while (num > 0);
            Console.WriteLine("BPM:{0}",60*1000*1000/usec);
        }
    }
}
