using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PRISMPLAYERLib;
using Excel = Microsoft.Office.Interop.Excel;
using System.Runtime.InteropServices;

namespace teamproject1
{
    public partial class OpendataPlayer : Form
    {
        /*--------------------------------------------------------------------------------
         * 変数
         *--------------------------------------------------------------------------------*/
        private PrismPlayer m_pplayer = null;
        private bool m_bTakeFlag = false;
        private bool CoronaFlag = true;
        private string m_strPreButtonName = "";
        private Color m_PreButtonColor=Color.Empty;
        private Color m_PreButtonBorderColor = Color.Empty;
        private ButtonBorderStyle m_PreButtonStyle = ButtonBorderStyle.Dashed;
        private Color[] m_aryLocalColors = new Color[]
        {
            Color.FromArgb(24,92,209),Color.FromArgb(34,168,245),Color.FromArgb(30,122,30),Color.FromArgb(54, 246,54),
            Color.FromArgb(226,209,48),Color.FromArgb(153,96,0),Color.FromArgb(255,106,76),Color.FromArgb(233,51,51)
        };
        private int[][] m_jaryLocalPrefectureIDs = new int[][]
        {//都道府県番号から地方分け
            new int[] {2},
            new int[] {3,4,5,6,7,8},
            new int[] {9,10,11,12,13,14,15},
            new int[] {16,17,18,19,20,21,22,23,24},
            new int[] {25,26,27,28,29,30,31},
            new int[] {32,33,34,35,36},
            new int[] {37,38,39,40},
            new int[] {41,42,43,44,45,46,47,48},
        };
        private System.Threading.Thread m_TakeThread = null;    //Take実行スレッド生成用
        private OpendataDownLoader.OpendataDownLoader m_OpendataDownLoader = null;
        private string m_strSceneDir = "";
        private byte m_WeatherFlag = 0x00;
        private int nPlay = 0;
        private System.Diagnostics.Process m_procGatherCsvExe = null;


        /*--------------------------------------------------------------------------------
         * コンストラクタ
         *--------------------------------------------------------------------------------*/
        public OpendataPlayer()
        {
            InitializeComponent();

            this.m_strSceneDir = $"{AppDomain.CurrentDomain.BaseDirectory}";
            m_strSceneDir = m_strSceneDir.Substring(0, m_strSceneDir.IndexOf(@"\bin"));
            m_strSceneDir = m_strSceneDir.Substring(0, m_strSceneDir.LastIndexOf(@"\")) + @"\シーン\";

            // オープンデータ更新監視用fileSystemWatcher設定
            string strDataPath = this.m_strSceneDir + "Data";
            this.OpendataFileWatcher.Path = strDataPath;
            this.OpendataFileWatcher.Renamed += new System.IO.RenamedEventHandler(watcher_Renamed);

            MonthCalendar.MaxDate = getLatestDate(strDataPath + @"\新規陽性者数.csv");

            // 天気情報.csv作成用プロセスの設定.
            string strGatherCsvExePath = $"{AppDomain.CurrentDomain.BaseDirectory}";
            strGatherCsvExePath = strGatherCsvExePath.Substring(0, strGatherCsvExePath.LastIndexOf("teamproject1")) + @"OpendataDownLoader\GatherWeatherCsv\GatherWeatherCsv\bin\Debug\GatherWeatherCsv.exe";

            m_procGatherCsvExe = new System.Diagnostics.Process();
            m_procGatherCsvExe.StartInfo.FileName = strGatherCsvExePath;
            m_procGatherCsvExe.StartInfo.CreateNoWindow = true;
            m_procGatherCsvExe.StartInfo.UseShellExecute = false;
            m_procGatherCsvExe.EnableRaisingEvents = true;
            m_procGatherCsvExe.Exited += new System.EventHandler(LoadScheme);
        }

        /*--------------------------------------------------------------------------------
         * 読み込み時に呼ばれます。
         *--------------------------------------------------------------------------------*/
        private void OpendataPlayer_Load(object sender, EventArgs e)
        {
            try
            {
                m_OpendataDownLoader = new OpendataDownLoader.OpendataDownLoader();
                if (!m_OpendataDownLoader.setupOpendataPlayer())
                {
                    m_OpendataDownLoader.deleteTask();
                    m_OpendataDownLoader = null;
                    MessageBox.Show(this, "オープンデータ自動取得プログラムの設定に失敗しました", "caption", MessageBoxButtons.OK);
                    Close();
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "caption", MessageBoxButtons.OK);
            }

            m_pplayer = new PrismPlayer();
            int ret = m_pplayer.initialize();
            if (ret == -1)
            {
                m_pplayer = null;
                MessageBox.Show(this, "Prismの初期化に失敗しました", "caption", MessageBoxButtons.OK);
                Close();
                return;
            }
            // OAStateのイベントハンドラ設定
            m_pplayer.onChangeOAState += new _IPrismEvents_onChangeOAStateEventHandler(IsWaitingtoTake_onChangeOAState);

            //送出デバイスの設定
            m_pplayer.execute("GetDevice WinGL HD SendTo -1 0");

            LoadScheme(sender, e);
            if (CoronaFlag)
            {
                Corona.BackgroundImage = Properties.Resources.covid19button_put;
                Weather.BackgroundImage = Properties.Resources.weatherbutton;
                textBox1.AppendText( "《コロナ》\r\n");
            }
        }

        /*--------------------------------------------------------------------------------
         * 終了時に呼ばれます。
         *--------------------------------------------------------------------------------*/
        private void OpendataPlayer_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (m_pplayer != null)
            {
                m_pplayer.execute("Abort B");

                m_pplayer.execute("Unload");
                m_pplayer = null;
            }

            if (OpendataFileWatcher != null)
            {
                OpendataFileWatcher.EnableRaisingEvents = false;
                OpendataFileWatcher.Dispose();
                OpendataFileWatcher = null;
            }

            m_OpendataDownLoader.deleteTask();

            m_procGatherCsvExe.Close();
        }

        /*--------------------------------------------------------------------------------
         * 最新日付取得メソッド.
         *--------------------------------------------------------------------------------*/
        private DateTime getLatestDate(string strCovidPath)
        {
            DateTime ret = DateTime.Today.AddDays(-1);
            
            if (System.IO.File.Exists(strCovidPath))
            {
                string strAllText = System.IO.File.ReadAllText(strCovidPath, System.Text.Encoding.GetEncoding("shift-jis"));
                string[] arySplitesText = strAllText.Replace('\n', ',').Split(',');
                Array.Reverse(arySplitesText);
                DateTime dtLatestDate = DateTime.ParseExact(arySplitesText[49], "yyyy/MM/dd", null);
                ret = dtLatestDate;
            }
            return ret;
        }

        private static System.Threading.Timer CovidAreaTimer;
        //private static System.Threading.Timer WeatherAreaTimer;

        /*--------------------------------------------------------------------------------
         * 自動再生ボタン.
         *--------------------------------------------------------------------------------*/
        private void Play_Click(object sender, EventArgs e)
        {
            m_pplayer.execute("Abort B");
            m_pplayer.execute("Clear B");
            //TextBox表示
            textBox1.AppendText( "【自動再生中】\r\n");
            //再生停止ボタンの切り替え
            Play.Visible = false;
            Stop.Visible = true;
            // 指定秒数間隔で呼び出される処理
            TimerCallback callback = state =>
            {
                if (CoronaFlag)
                {//コロナ表示
                    switch (nPlay)
                    {
                        case 0:
                            m_pplayer.execute("Play '北海道'");
                            nPlay++;
                            break;
                        case 1:
                            m_pplayer.execute("Play '東北'");
                            nPlay++;
                            break;
                        case 2:
                            m_pplayer.execute("Play '関東'");
                            nPlay++;
                            break;
                        case 3:
                            m_pplayer.execute("Play '中部'");
                            nPlay++;
                            break;
                        case 4:
                            m_pplayer.execute("Play '近畿'");
                            nPlay++;
                            break;
                        case 5:
                            m_pplayer.execute("Play '中国'");
                            nPlay++;
                            break;
                        case 6:
                            m_pplayer.execute("Play '四国'");
                            nPlay++;
                            break;
                        default:
                            m_pplayer.execute("Play '九州'");
                            nPlay = 0;
                            break;
                    }
                }
                else
                {
                    //天気情報表示
                    switch (nPlay)
                    {
                        case 0:
                            m_pplayer.execute("Play '全国の天気情報'");
                            Console.WriteLine("全国天気");
                            nPlay++;
                            break;
                        case 1:
                            m_pplayer.execute("Play '北海道天気'");
                            Console.WriteLine("北海道天気");
                            nPlay++;
                            break;
                        case 2:
                            m_pplayer.execute("Play '東北天気'");
                            Console.WriteLine("東北天気");
                            nPlay++;
                            break;
                        case 3:
                            m_pplayer.execute("Play '関東天気'");
                            Console.WriteLine("関東天気");
                            nPlay++;
                            break;
                        case 4:
                            m_pplayer.execute("Play '中部天気'");
                            Console.WriteLine("中部天気");
                            nPlay++;
                            break;
                        case 5:
                            m_pplayer.execute("Play '関西天気'");
                            Console.WriteLine("近畿天気");
                            nPlay++;
                            break;
                        case 6:
                            m_pplayer.execute("Play '中国天気'");
                            Console.WriteLine("中国天気");
                            nPlay++;
                            break;
                        case 7:
                            m_pplayer.execute("Play '四国天気'");
                            Console.WriteLine("四国天気");
                            nPlay++;
                            break;
                        default:
                            m_pplayer.execute("Play '九州天気'");
                            Console.WriteLine("九州天気");
                            nPlay = 0;
                            break;
                    }
                }
                // Takeを別スレッドで実行
                m_TakeThread = new System.Threading.Thread(new System.Threading.ThreadStart(TakeThread));
                m_TakeThread.Start();
            };
            // タイマー起動(0秒後に処理実行、5秒おきに繰り返し)
            if (CoronaFlag)
            {
                CovidAreaTimer = new System.Threading.Timer(callback, null, 0, 7500);
            }
            else
            {
                CovidAreaTimer = new System.Threading.Timer(callback, null, 0, 14000);
            }
            
        }



        /*--------------------------------------------------------------------------------
         * 再生停止ボタン.
         *--------------------------------------------------------------------------------*/
        private void Stop_Click(object sender, EventArgs e)
        {
            //TextBoxに表示
            textBox1.AppendText( "【停止】\r\n");
            //再生停止の切り替え
            Play.Visible = true;
            Stop.Visible = false;
            //タイマー停止
            CovidAreaTimer.Dispose();
        }

        /*--------------------------------------------------------------------------------
         * スキーマの読み込み.
         *--------------------------------------------------------------------------------*/
        private void LoadScheme(object sender, EventArgs e)
        {
            string strSchemePath = m_strSceneDir.Replace(@"\", @"\\");
            m_pplayer.execute("Load '" + strSchemePath + @"Scn\\TeamDevelopment.scm'");
        }

        /*--------------------------------------------------------------------------------
         * OAStateのイベント時に呼ばれます。
         * Take待ち状態ならTakeスレッドのフラグを立てる。
         *--------------------------------------------------------------------------------*/
        private void IsWaitingtoTake_onChangeOAState(String bstrDevice, int lOAState, int lOAType)
        {
            //テイクできるかどうか
            m_bTakeFlag = (lOAState == 1) ? true : false;
        }

        /*--------------------------------------------------------------------------------
         * Takeができるまで実行するスレッド。
         * システム変数操作ボタン押下時に呼ばれます。
         *--------------------------------------------------------------------------------*/
        private async void TakeThread()
        {
            //テイク待ちになるまで
            while (!m_bTakeFlag)
            {
                System.Diagnostics.Debug.WriteLine("ログテスト\n");
                await Task.Delay(10);
            }
            // 送出
            m_pplayer.execute("Take");
            return;
        }



        private string selectDate;
        /*--------------------------------------------------------------------------------
         * 指定した日付・都道府県のコロナ感染者を表示
         *--------------------------------------------------------------------------------*/
        private void NihonMethod(object sender, EventArgs e)
        {
            m_pplayer.execute("Abort B");
            m_pplayer.execute("Clear B");
            //一つ前に押したボタンのデザインを戻す処理
            //押されたボタンの名前を記憶
            string strSenderName = ((Button)sender).Name;
            int PrefectureNumber = int.Parse(strSenderName.Substring(2));
            Console.WriteLine(strSenderName.Substring(2));
            int Localid = Localjudge(PrefectureNumber);
            //Console.WriteLine(Localid);
            Control control = null;

            //ひとつ前に押されたボタンの名前が保存されているとき
            //ひとつ前に押されたボタンの名前が空欄じゃないとき
            if (m_strPreButtonName != "")
            {
                //ひとつ前に押されたボタンの機能を記憶
                control = this.Controls[m_strPreButtonName];
                int PreButtonNumber = int.Parse(m_strPreButtonName.Substring(2));
                //押されたボタンを元に戻す
                if (PreButtonNumber == 2)
                {
                    control = this.Controls["id2"];
                    ((Button)control).BackColor = m_PreButtonColor;
                    ((Button)control).FlatAppearance.BorderColor = m_PreButtonBorderColor;
                    ((Button)control).FlatAppearance.BorderSize = (int)m_PreButtonStyle;
                }
                else if (3 <= PreButtonNumber && PreButtonNumber <= 8)
                {
                    for (int Num = 3; Num <= 8; Num++)
                    {
                        control = this.Controls["id" + Num.ToString()];
                        ((Button)control).BackColor = m_PreButtonColor;
                        ((Button)control).FlatAppearance.BorderColor = m_PreButtonBorderColor;
                        ((Button)control).FlatAppearance.BorderSize = (int)m_PreButtonStyle;
                    }
                }
                else if (9 <= PreButtonNumber && PreButtonNumber <= 15)
                {
                    for (int Num = 9; Num <= 15; Num++)
                    {
                        control = this.Controls["id" + Num.ToString()];
                        ((Button)control).BackColor = m_PreButtonColor;
                        ((Button)control).FlatAppearance.BorderColor = m_PreButtonBorderColor;
                        ((Button)control).FlatAppearance.BorderSize = (int)m_PreButtonStyle;
                    }
                }
                else if (16 <= PreButtonNumber && PreButtonNumber <= 24)
                {
                    for (int Num = 16; Num <= 24; Num++)
                    {
                        control = this.Controls["id" + Num.ToString()];
                        ((Button)control).BackColor = m_PreButtonColor;
                        ((Button)control).FlatAppearance.BorderColor = m_PreButtonBorderColor;
                        ((Button)control).FlatAppearance.BorderSize = (int)m_PreButtonStyle;
                    }
                }
                else if (25 <= PreButtonNumber && PreButtonNumber <= 31)
                {
                    for (int Num = 25; Num <= 31; Num++)
                    {
                        control = this.Controls["id" + Num.ToString()];
                        ((Button)control).BackColor = m_PreButtonColor;
                        ((Button)control).FlatAppearance.BorderColor = m_PreButtonBorderColor;
                        ((Button)control).FlatAppearance.BorderSize = (int)m_PreButtonStyle;
                    }
                }
                else if (32 <= PreButtonNumber && PreButtonNumber <= 36)
                {
                    for (int Num = 32; Num <= 36; Num++)
                    {
                        control = this.Controls["id" + Num.ToString()];
                        ((Button)control).BackColor = m_PreButtonColor;
                        ((Button)control).FlatAppearance.BorderColor = m_PreButtonBorderColor;
                        ((Button)control).FlatAppearance.BorderSize = (int)m_PreButtonStyle;
                    }
                }
                else if (37 <= PreButtonNumber && PreButtonNumber <= 40)
                {
                    for (int Num = 37; Num <= 40; Num++)
                    {
                        control = this.Controls["id" + Num.ToString()];
                        ((Button)control).BackColor = m_PreButtonColor;
                        ((Button)control).FlatAppearance.BorderColor = m_PreButtonBorderColor;
                        ((Button)control).FlatAppearance.BorderSize = (int)m_PreButtonStyle;
                    }
                }
                else if (41 <= PreButtonNumber && PreButtonNumber <= 48)
                {
                    for (int Num = 41; Num <= 48; Num++)
                    {
                        control = this.Controls["id" + Num.ToString()];
                        ((Button)control).BackColor = m_PreButtonColor;
                        ((Button)control).FlatAppearance.BorderColor = m_PreButtonBorderColor;
                        ((Button)control).FlatAppearance.BorderSize = (int)m_PreButtonStyle;
                    }
                }
                ((Button)control).UseVisualStyleBackColor = true;                
            }
            m_strPreButtonName = strSenderName;
            m_PreButtonColor = ((Button)sender).BackColor;
            //id番号の検出
            string strAreaName = "";           

            if (CoronaFlag)
            {//コロナ表示
                //TextBoxに都道府県名を表示
                textBox1.AppendText( ((Button)sender).Text+"\r\n");
                //押したボタンの色変更
                if (PrefectureNumber == 2)
                {
                    ((Button)sender).BackColor = Color.FromArgb(24,92,209);
                }
                else if (3 <= PrefectureNumber && PrefectureNumber <= 8)
                {
                    ((Button)sender).BackColor = Color.FromArgb(34,168,245);
                }
                else if (9 <= PrefectureNumber && PrefectureNumber <= 15)
                {
                    ((Button)sender).BackColor = Color.FromArgb(30,122,30);
                }
                else if (16 <= PrefectureNumber && PrefectureNumber <= 24)
                {
                    ((Button)sender).BackColor = Color.FromArgb(54, 246,54);
                }
                else if (25 <= PrefectureNumber && PrefectureNumber <= 31)
                {
                    ((Button)sender).BackColor = Color.FromArgb(226,209,48);
                }
                else if (32 <= PrefectureNumber && PrefectureNumber <= 36)
                {
                    ((Button)sender).BackColor = Color.FromArgb(153,96,0);
                }
                else if (37 <= PrefectureNumber && PrefectureNumber <= 40)
                {
                    ((Button)sender).BackColor = Color.FromArgb(255,106,76);
                }
                else if (41 <= PrefectureNumber && PrefectureNumber <= 48)
                {
                    ((Button)sender).BackColor = Color.FromArgb(233,51,51);
                }
                ((Button)sender).UseVisualStyleBackColor = true;
                m_pplayer.execute("Set V0 " + strSenderName.Substring(2));
                Console.WriteLine(strSenderName);
                Console.WriteLine(strSenderName.Substring(2));
                m_pplayer.execute("Set V1 '" + selectDate + "'");
                m_pplayer.execute("Play '日本地図'");
                // Takeを別スレッドで実行
                m_TakeThread = new System.Threading.Thread(new System.Threading.ThreadStart(TakeThread));
                m_TakeThread.Start();
            }
            else
            {//天気予報表示  
                switch (Localid)
                {
                    case 0:
                        control = this.Controls["id2"];
                        ((Button)control).FlatAppearance.BorderColor = Color.FromArgb(24, 92, 209);
                        ((Button)control).FlatAppearance.BorderSize = 7;
                        strAreaName = "北海道天気";
                        textBox1.AppendText( strAreaName.Substring(0, 3)+"地方\r\n");
                        break;
                    case 1:
                        for (int Num = 3; Num <= 8; Num++)
                        {
                            control = this.Controls["id" + Num.ToString()];
                            ((Button)control).FlatAppearance.BorderColor = Color.FromArgb(34, 168, 245);
                            ((Button)control).FlatAppearance.BorderSize = 7;    
                        }
                        strAreaName = "東北天気";
                        textBox1.AppendText( strAreaName.Substring(0, 2) + "地方\r\n");
                        break;
                    case 2:
                        for (int Num = 9; Num <= 15; Num++)
                        {
                            control = this.Controls["id" + Num.ToString()];
                            ((Button)control).FlatAppearance.BorderColor = Color.FromArgb(30, 122, 30);
                            ((Button)control).FlatAppearance.BorderSize = 7;  
                        }
                        strAreaName = "関東天気";
                        textBox1.AppendText( strAreaName.Substring(0, 2) + "地方\r\n");
                        break;
                    case 3:
                        for (int Num = 16; Num <= 24; Num++)
                        {
                            control = this.Controls["id" + Num.ToString()];
                            ((Button)control).FlatAppearance.BorderColor = Color.FromArgb(54, 246, 54);
                            ((Button)control).FlatAppearance.BorderSize = 7;  
                        }
                        strAreaName = "中部天気";
                        textBox1.AppendText( strAreaName.Substring(0, 2) + "地方\r\n");
                        break;
                    case 4:
                        for (int Num = 25; Num <= 31; Num++)
                        {
                            control = this.Controls["id" + Num.ToString()];
                            ((Button)control).FlatAppearance.BorderColor = Color.FromArgb(226, 209, 48);
                            ((Button)control).FlatAppearance.BorderSize = 7;  
                        }
                        strAreaName = "近畿天気";
                        textBox1.AppendText( strAreaName.Substring(0, 2) + "地方\r\n");
                        break;
                    case 5:
                        for (int Num = 32; Num <= 36; Num++)
                        {
                            control = this.Controls["id" + Num.ToString()];
                            ((Button)control).FlatAppearance.BorderColor = Color.FromArgb(153, 96, 0);
                            ((Button)control).FlatAppearance.BorderSize = 7;   
                        }
                        strAreaName = "中国天気";
                        textBox1.AppendText( strAreaName.Substring(0, 2) + "地方\r\n");
                        break;
                    case 6:
                        for (int Num = 37; Num <= 40; Num++)
                        {
                            control = this.Controls["id" + Num.ToString()];
                            ((Button)control).FlatAppearance.BorderColor = Color.FromArgb(255, 106, 76);
                            ((Button)control).FlatAppearance.BorderSize = 7;   
                        }
                        strAreaName = "四国天気";
                        textBox1.AppendText( strAreaName.Substring(0, 2) + "地方\r\n");
                        break;
                    case 7:
                        for (int Num = 41; Num <= 48; Num++)
                        {
                            control = this.Controls["id" + Num.ToString()];
                            ((Button)control).FlatAppearance.BorderColor = Color.FromArgb(233, 51, 51);
                            ((Button)control).FlatAppearance.BorderSize = 7;  
                        }
                        strAreaName = "九州天気";
                        textBox1.AppendText( strAreaName.Substring(0, 2) + "地方\r\n");
                        break;
                }
                ((Button)sender).UseVisualStyleBackColor = true;
                m_pplayer.execute("Play '" + strAreaName + "'");
                // Takeを別スレッドで実行
                m_TakeThread = new System.Threading.Thread(new System.Threading.ThreadStart(TakeThread));
                m_TakeThread.Start();
            }
        }


        private int Localjudge(int PreNumber)
        {
            int ret = -1;

            if (PreNumber == 2)
            {
                ret=0;
            }
            else if (3 <= PreNumber && PreNumber <= 8)
            {
                ret = 1;
            }
            else if (9 <= PreNumber && PreNumber <= 15)
            {
                ret = 2;
            }
            else if (16 <= PreNumber && PreNumber <= 24)
            {
                ret = 3;
            }
            else if (25 <= PreNumber && PreNumber <= 31)
            {
                ret = 4;
            }
            else if (32 <= PreNumber && PreNumber <= 36)
            {
                ret = 5;
            }
            else if (37 <= PreNumber && PreNumber <= 40)
            {
                ret = 6;
            }
            else if (41 <= PreNumber && PreNumber <= 48)
            {
                ret = 7;
            }
            return ret;
        }
        /*--------------------------------------------------------------------------------
         * 
         *--------------------------------------------------------------------------------*/
        private void MonthCalendar_DateChanged(object sender, DateRangeEventArgs e)
        {
            //選択した日付を出力
            selectDate = MonthCalendar.SelectionStart.ToShortDateString();
            Console.WriteLine(MonthCalendar.SelectionStart.ToShortDateString());
            textBox1.AppendText(MonthCalendar.SelectionStart.ToShortDateString() + "\r\n");
        }

        /*--------------------------------------------------------------------------------
         * 天気ボタンでボタン画像の切り替え・カレンダー機能の停止
         *--------------------------------------------------------------------------------*/
        private void Weather_Click(object sender, EventArgs e)
        {
            CoronaFlag = false;
            MonthCalendar.Enabled = false;
            Corona.BackgroundImage = Properties.Resources.covid19button;
            Weather.BackgroundImage = Properties.Resources.weatherbutton_put;
            textBox1.AppendText("《天気》\r\n");
            //自動のリセット
            nPlay = 0;
        }

        /*--------------------------------------------------------------------------------
         * コロナボタンで画像の切り替え・カレンダー作動
         *--------------------------------------------------------------------------------*/
        private void Corona_Click(object sender, EventArgs e)
        {
            CoronaFlag = true;
            MonthCalendar.Enabled = true;
            Corona.BackgroundImage = Properties.Resources.covid19button_put;
            Weather.BackgroundImage = Properties.Resources.weatherbutton;
            textBox1.AppendText( "《コロナ》\r\n");
            //自動のリセット
            nPlay = 0;
        }

        /*--------------------------------------------------------------------------------
         * オープンデータ保存ディレクトリ内に変更が起きた時に呼び出されるイベント.
         * スキーマを読み込みなおす.
         * 天気情報をまとめたファイルを作成する.
         *--------------------------------------------------------------------------------*/
        private void watcher_Renamed(object sender, System.IO.FileSystemEventArgs e)
        {
            if (e.Name == "新規陽性者数.csv")
            {
                // 新規陽性者数.csvが更新された時の処理
                MonthCalendar.MaxDate = getLatestDate(e.FullPath);
                LoadScheme(null, null);
            }
            else
            {
                // 天気情報の更新処理
                // 全ての天気に関するファイルが更新されたら、まとめる処理を実行.
                switch(e.Name)
                {
                    case "最高気温.csv":
                        m_WeatherFlag |= 0x01;
                        break;
                    case "最低気温.csv":
                        m_WeatherFlag |= 0x02;
                        break;
                    case "日降水量.csv":
                        m_WeatherFlag |= 0x04;
                        break;
                    case "最大風速.csv":
                        m_WeatherFlag |= 0x08;
                        break;
                    default:
                        break;
                }

                if(m_WeatherFlag == 0x0f)
                {
                    // 4種類の天気情報すべてが更新された時の処理
                    try
                    {
                        // 別exeファイル（天気情報をまとめるプログラム）を実行.
                        m_procGatherCsvExe.Start();

                        m_WeatherFlag = 0x00;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
                }
            }
        }
    }
}