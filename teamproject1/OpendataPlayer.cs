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
        private string[][] m_jaryScnNames = new string[][]
        {
            new string[] { "北海道", "東北", "関東", "中部", "近畿", "中国", "四国", "九州"},
            new string[] { "全国の天気情報", "北海道天気", "東北天気", "関東天気", "中部天気", "近畿天気", "中国天気", "四国天気", "九州天気"}
        };
        private string m_strOldButtonName = "";
        private Color m_OldButtonColor=Color.Empty;
        private Color m_OldButtonBorderColor = Color.Empty;
        private ButtonBorderStyle m_OldButtonStyle = ButtonBorderStyle.Dashed;
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

        private static System.Threading.Timer AreaTimer;
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
                string strScnName = "";
                if(CoronaFlag)
                {
                    strScnName = m_jaryScnNames[0][nPlay];
                    nPlay = (nPlay < m_jaryScnNames[0].Length - 1) ? ++nPlay : 0;
                }
                else
                {
                    strScnName = m_jaryScnNames[1][nPlay];
                    nPlay = (nPlay < m_jaryScnNames[1].Length - 1) ? ++nPlay : 0;
                    Console.WriteLine(strScnName);
                }

                m_pplayer.execute("Play '" + strScnName + "'");
                // Takeを別スレッドで実行
                m_TakeThread = new System.Threading.Thread(new System.Threading.ThreadStart(TakeThread));
                m_TakeThread.Start();                
            };

            // タイマー起動(0秒後に処理実行、5秒おきに繰り返し)
            AreaTimer = CoronaFlag ? new System.Threading.Timer(callback, null, 0, 7500) : new System.Threading.Timer(callback, null, 0, 14000);            
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
            AreaTimer.Dispose();
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
            //押されたボタンの名前とIDを記憶
            string strSenderName = ((Button)sender).Name;
            int PrefectureNumber = int.Parse(strSenderName.Substring(2));
            Console.WriteLine(PrefectureNumber);

            // 押されたボタンの地方を取得
            int nLocalID = Localjudge(PrefectureNumber);
            //Console.WriteLine(Localid);
 
            Control control = null;

            //ひとつ前に押されたボタンの名前が保存されているとき
            if (m_strOldButtonName != "")
            {
                //ひとつ前に押されたボタンの機能を記憶
                int OldButtonNumber = int.Parse(m_strOldButtonName.Substring(2));
                int nOldLocalID = Localjudge(OldButtonNumber);

                //押されたボタンを元に戻す
                // 押されたボタンの地方の各都道府県のID配列を取得
                int[] aryOldLocalPrefectureIDs = m_jaryLocalPrefectureIDs[nOldLocalID];
                foreach(int PrefectureID in aryOldLocalPrefectureIDs)
                {
                    control = this.Controls["id" + PrefectureID.ToString()];
                    ((Button)control).BackColor = m_OldButtonColor;
                    ((Button)control).FlatAppearance.BorderColor = m_OldButtonBorderColor;
                    ((Button)control).FlatAppearance.BorderSize = (int)m_OldButtonStyle;
                    ((Button)control).UseVisualStyleBackColor = true;
                }                
            }

            // 今押されたボタンを一つ前のボタンとして記憶
            m_strOldButtonName = strSenderName;
            m_OldButtonColor = ((Button)sender).BackColor;
            
            //id番号の検出
            string strAreaName = "";

            if (CoronaFlag)
            {//コロナ表示
                //TextBoxに都道府県名を表示
                textBox1.AppendText( ((Button)sender).Text+"\r\n");

                //押したボタンの色変更
                ((Button)sender).BackColor = m_aryLocalColors[nLocalID];
                ((Button)sender).UseVisualStyleBackColor = true;

                m_pplayer.execute("Set V0 " + strSenderName.Substring(2));
                Console.WriteLine(strSenderName);
                Console.WriteLine(strSenderName.Substring(2));
                m_pplayer.execute("Set V1 '" + selectDate + "'");
                m_pplayer.execute("Play '日本地図'");
            }
            else
            {//天気予報表示  
                int[] aryLocalPrefectureIDs = m_jaryLocalPrefectureIDs[nLocalID];
                foreach (int PrefectureID in aryLocalPrefectureIDs)
                {
                    control = this.Controls["id" + PrefectureID.ToString()];
                    ((Button)control).FlatAppearance.BorderColor = m_aryLocalColors[nLocalID];
                    ((Button)control).FlatAppearance.BorderSize = 7;
                    ((Button)sender).UseVisualStyleBackColor = true;
                }
                strAreaName = m_jaryScnNames[1][nLocalID + 1];
                textBox1.AppendText(strAreaName.Replace("天気", "地方") + "\r\n");
                m_pplayer.execute("Play '" + strAreaName + "'");
            }
            
            // Takeを別スレッドで実行
            m_TakeThread = new System.Threading.Thread(new System.Threading.ThreadStart(TakeThread));
            m_TakeThread.Start();
        }


        private int Localjudge(int PrefectureID)
        {
            int retLocalID = -1;

            if (PrefectureID == 2)
            {
                retLocalID = 0;
            }
            else if (3 <= PrefectureID && PrefectureID <= 8)
            {
                retLocalID = 1;
            }
            else if (9 <= PrefectureID && PrefectureID <= 15)
            {
                retLocalID = 2;
            }
            else if (16 <= PrefectureID && PrefectureID <= 24)
            {
                retLocalID = 3;
            }
            else if (25 <= PrefectureID && PrefectureID <= 31)
            {
                retLocalID = 4;
            }
            else if (32 <= PrefectureID && PrefectureID <= 36)
            {
                retLocalID = 5;
            }
            else if (37 <= PrefectureID && PrefectureID <= 40)
            {
                retLocalID = 6;
            }
            else if (41 <= PrefectureID && PrefectureID <= 48)
            {
                retLocalID = 7;
            }
            return retLocalID;
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