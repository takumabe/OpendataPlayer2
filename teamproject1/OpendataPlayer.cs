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
        private System.Threading.Thread m_TakeThread = null;    //Take実行スレッド生成用
        private OpendataDownLoader.OpendataDownLoader m_OpendataDownLoader = null;  // 自動ダウンロード設定をするクラス
        private System.Diagnostics.Process m_procFormatCsvExe = null;   // 新規陽性者数.csvの書式を整えるプログラムを実行するプロセス
        private System.Diagnostics.Process m_procGatherCsvExe = null;   // 天気情報をまとめるプログラムを実行するプロセス

        private string m_strSelectDate = "";    // 選択中の日付保存用
        private string m_strSceneDir = "";      // シーンディレクトリのパス

        private bool m_bTakeFlag = false;       // Takeできるかどうか
        private bool m_bCoronaFlag = true;      // コロナか天気か
        private bool m_bCsvExistFlag = false;   // 新規陽性者数.csvと天気情報.csvが存在しているか
        private byte m_WeatherCsvFlag = 0x00;   // 天気系csvファイルのどれが存在しているか

        private static System.Threading.Timer m_AreaTimer;      // 自動再生用タイマ
        private int m_nPlayCount = 0;                           // 自動再生の繰り返しカウンタ

        private string m_strOldButtonName = "";                                 // 一つ前に押された都道府県ボタンの名前
        private Color m_OldButtonColor = Color.Empty;                           // 一つ前に押されたボタンの色
        private Color m_OldButtonBorderColor = Color.Empty;                     // 一つ前に押されたボタンの枠の色
        private ButtonBorderStyle m_OldButtonStyle = ButtonBorderStyle.Dashed;  // 一つ前に押されたボタンの枠の太さ

        private string[][] m_jaryScnNames = new string[][]
        {
            new string[] { "北海道", "東北", "関東", "中部", "近畿", "中国", "四国", "九州"},    // コロナ自動再生用シーン名
            new string[] { "全国の天気情報", "北海道天気", "東北天気", "関東天気", "中部天気", "近畿天気", "中国天気", "四国天気", "九州天気"}  // 天気自動再生用シーン名
        };
        private Color[][] m_jaryLocalColors = new Color[][]
        {
            //各地方の標準色
            new Color[]{Color.CornflowerBlue ,Color.LightSkyBlue,Color.LimeGreen,Color.PaleGreen,
                Color.Khaki,Color.Orange,Color.LightPink,Color.FromArgb(244, 146, 146) },
            // 各地方の押された時の色
            new Color[] { Color.FromArgb(24,92,209),Color.FromArgb(34,168,245),Color.FromArgb(30,122,30),Color.FromArgb(54, 246,54),
            Color.FromArgb(226,209,48),Color.FromArgb(153,96,0),Color.FromArgb(255,106,76),Color.FromArgb(233,51,51) }
        };
        private int[][] m_jaryLocalPrefectureIDs = new int[][]
        {
            //都道府県番号から地方分け
            new int[] {2},                              // 北海道地方
            new int[] {3,4,5,6,7,8},                    // 東北地方
            new int[] {9,10,11,12,13,14,15},            // 関東地方
            new int[] {16,17,18,19,20,21,22,23,24},     // 中部地方
            new int[] {25,26,27,28,29,30,31},           // 近畿地方
            new int[] {32,33,34,35,36},                 // 中国地方
            new int[] {37,38,39,40},                    // 四国地方
            new int[] {41,42,43,44,45,46,47,48}         // 九州地方
        };
        private int[][] m_jarySpotIDs = new int[][]
        {
            // 各都道府県の県庁所在地の観測所番号リスト.
            new int[] {14163,34392,54232,44132,51106,57066, 62078, 67437, 74181, 82182, 88317, 91197 },     // 全国の天気情報シーンに表示される観測所番号
            new int[] {14163,19432},                                                // 北海道天気の観測所番号
            new int[] {31312,33431,34392,32402,35426,36127},                        // 東北天気の観測所番号
            new int[] {40201,41277,42251,43241,45212,44132,46106},                  // 関東天気の観測所番号
            new int[] {54232,55102,56227,57066,49142,48156,52586,50331,51106},      // 中部天気の観測所番号
            new int[] {53133,60216,61286,62078,63518,64036,65042},                  // 近畿天気の観測所番号
            new int[] {69122,68132,66408,67437,81286},                              // 中国天気の観測所番号
            new int[] {71106,72086,73166,74181},                                    // 四国天気の観測所番号
            new int[] {82182,85142,84496,86141,83216,87376,88317,91197}             // 九州天気の観測所番号
        };


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

            MonthCalendar.MaxDate = getLatestDate($@"{strDataPath}\新規陽性者数.csv");

            // 新規陽性者数.csv作成用プロセスの設定.
            string strFormatCsvExePath = $"{AppDomain.CurrentDomain.BaseDirectory}";
            strFormatCsvExePath = strFormatCsvExePath.Substring(0, strFormatCsvExePath.LastIndexOf("teamproject1")) + @"OpendataDownLoader\FormatCovidCsv\FormatCovidCsv\bin\Debug\FormatCovidCsv.exe";
            m_procFormatCsvExe = new System.Diagnostics.Process();
            m_procFormatCsvExe.StartInfo.FileName = strFormatCsvExePath;
            m_procFormatCsvExe.StartInfo.CreateNoWindow = true;
            m_procFormatCsvExe.StartInfo.UseShellExecute = false;
            m_procFormatCsvExe.EnableRaisingEvents = true;
            m_procFormatCsvExe.Exited += new System.EventHandler(UpdatedCovidData);

            // 天気情報.csv作成用プロセスの設定.
            string strGatherCsvExePath = $"{AppDomain.CurrentDomain.BaseDirectory}";
            strGatherCsvExePath = strGatherCsvExePath.Substring(0, strGatherCsvExePath.LastIndexOf("teamproject1")) + @"OpendataDownLoader\GatherWeatherCsv\GatherWeatherCsv\bin\Debug\GatherWeatherCsv.exe";
            m_procGatherCsvExe = new System.Diagnostics.Process();
            m_procGatherCsvExe.StartInfo.FileName = strGatherCsvExePath;
            m_procGatherCsvExe.StartInfo.CreateNoWindow = true;
            m_procGatherCsvExe.StartInfo.UseShellExecute = false;
            m_procGatherCsvExe.EnableRaisingEvents = true;
            m_procGatherCsvExe.Exited += new System.EventHandler(UpdatedWeatherCsv);
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

            if (!System.IO.File.Exists($@"{m_strSceneDir}\Data\新規陽性者数.csv") || !System.IO.File.Exists($@"{m_strSceneDir}\Data\天気情報.csv"))
            {
                MonthCalendar.Visible = false;
                Corona.Enabled = false;
                Weather.Enabled = false;
                Play.Enabled = false;
                // 日本地図のボタンデザインの変更・機能停止
                foreach (int[] aryPrefectureIDs in m_jaryLocalPrefectureIDs)
                {
                    foreach (int nPrefectureID in aryPrefectureIDs)
                    {
                        Control control = this.Controls[$"id{nPrefectureID}"];
                        ((Button)control).Enabled = false;
                        ((Button)control).BackColor = Color.AliceBlue;
                    }
                }
                m_bCsvExistFlag = true;
                textBox1.AppendText("データダウンロード中\r\n");
            }
            else
            {
                Corona.BackgroundImage = Properties.Resources.covid19button_put;
                Weather.BackgroundImage = Properties.Resources.weatherbutton;
                textBox1.AppendText("《コロナ》\r\n");
                MonthCalendar.SelectionStart = MonthCalendar.MaxDate;
            }

            LoadScheme();
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
         * コロナボタンで画像の切り替え・カレンダー作動
         *--------------------------------------------------------------------------------*/
        private void Corona_Click(object sender, EventArgs e)
        {
            //ひとつ前に押されたボタンの名前が保存されているとき
            if (m_strOldButtonName != "")
            {//一つ前に押したボタンのデザインを戻す処理
                this.ResetButtonDesign();
                m_strOldButtonName = "";
            }
            m_bCoronaFlag = true;
            MonthCalendar.Visible = true;
            Corona.BackgroundImage = Properties.Resources.covid19button_put;
            Weather.BackgroundImage = Properties.Resources.weatherbutton;
            textBox1.AppendText("《コロナ》\r\n");
            //自動のリセット
            m_nPlayCount = 0;
        }

        /*--------------------------------------------------------------------------------
         * 天気ボタンでボタン画像の切り替え・カレンダー機能の停止
         *--------------------------------------------------------------------------------*/
        private void Weather_Click(object sender, EventArgs e)
        {
            //ひとつ前に押されたボタンの名前が保存されているとき
            if (m_strOldButtonName != "")
            {//一つ前に押したボタンのデザインを戻す処理
                this.ResetButtonDesign();
                m_strOldButtonName = "";
            }
            m_bCoronaFlag = false;
            MonthCalendar.Visible = false;
            Corona.BackgroundImage = Properties.Resources.covid19button;
            Weather.BackgroundImage = Properties.Resources.weatherbutton_put;
            textBox1.AppendText("《天気》\r\n");
            //自動のリセット
            m_nPlayCount = 0;
        }

        /*--------------------------------------------------------------------------------
         *  カレンダーで選択された日付を取得.
         *--------------------------------------------------------------------------------*/
        private void MonthCalendar_DateChanged(object sender, DateRangeEventArgs e)
        {
            //選択した日付を出力
            m_strSelectDate = MonthCalendar.SelectionStart.ToShortDateString();
            Console.WriteLine(MonthCalendar.SelectionStart.ToShortDateString());
            textBox1.AppendText(MonthCalendar.SelectionStart.ToShortDateString() + "\r\n");
        }

        /*--------------------------------------------------------------------------------
         * 自動再生ボタン.
         *--------------------------------------------------------------------------------*/
        private void Play_Click(object sender, EventArgs e)
        {
            m_pplayer.execute("Abort B");
            m_pplayer.execute("Clear B");

            //カレンダー非表示
            MonthCalendar.Visible = false;

            //ひとつ前に押されたボタンの名前が保存されているとき
            if (m_strOldButtonName != "")
            {//一つ前に押したボタンのデザインを戻す処理
                this.ResetButtonDesign();
                m_strOldButtonName = "";
            }

            // 日本地図のボタンデザインの変更・機能停止
            foreach (int[] aryPrefectureIDs in m_jaryLocalPrefectureIDs)
            {
                foreach (int nPrefectureID in aryPrefectureIDs)
                {
                    Control control = this.Controls[$"id{nPrefectureID}"];
                    ((Button)control).Enabled = false;
                    ((Button)control).BackColor = Color.AliceBlue;
                }
            }

            //TextBox表示
            textBox1.AppendText( "【自動再生中】\r\n");
            
            //再生停止ボタンの切り替え
            Play.Visible = false;
            Stop.Visible = true;

            // 再生中のジャンル変更禁止
            Corona.Enabled = false;
            Weather.Enabled = false;

            // 指定秒数間隔で呼び出される処理
            TimerCallback callback = state =>
            {
                string strScnName = "";
                if(m_bCoronaFlag)
                {
                    // シーン名取得
                    strScnName = m_jaryScnNames[0][m_nPlayCount];
                    // シーン順に再生、最後まで再生されたら最初から繰り返す
                    m_nPlayCount = (m_nPlayCount < m_jaryScnNames[0].Length - 1) ? ++m_nPlayCount : 0;
                }
                else
                {
                    strScnName = m_jaryScnNames[1][m_nPlayCount];
                    m_nPlayCount = (m_nPlayCount < m_jaryScnNames[1].Length - 1) ? ++m_nPlayCount : 0;
                    Console.WriteLine(strScnName);
                }

                m_pplayer.execute("Play '" + strScnName + "'");
                // Takeを別スレッドで実行
                m_TakeThread = new System.Threading.Thread(new System.Threading.ThreadStart(TakeThread));
                m_TakeThread.Start();

                // 操作画面に情報表示
                DispData(m_nPlayCount);
            };

            // タイマー起動(0秒後に処理実行、5秒おきに繰り返し)
            m_AreaTimer = m_bCoronaFlag ? new System.Threading.Timer(callback, null, 0, 7500) : new System.Threading.Timer(callback, null, 0, 14000);            
        }

        /*--------------------------------------------------------------------------------
         * 再生停止ボタン.
         *--------------------------------------------------------------------------------*/
        private void Stop_Click(object sender, EventArgs e)
        {
            // ジャンル変更解禁
            Corona.Enabled = true;
            Weather.Enabled = true;

            //カレンダー表示
            if (m_bCoronaFlag)
            {
                MonthCalendar.Visible = true;
            }
            //ボタンの色を戻す
            foreach (int[] aryPrefectureIDs in m_jaryLocalPrefectureIDs)
            {
                foreach (int nPrefectureID in aryPrefectureIDs)
                {
                    Control control = this.Controls[$"id{nPrefectureID}"];
                    ((Button)control).Enabled = true;
                    int nLocalID = Localjudge(nPrefectureID);
                    ((Button)control).BackColor = m_jaryLocalColors[0][nLocalID];
                }
            }
            //TextBoxに表示
            textBox1.AppendText( "【停止】\r\n");
            //再生停止の切り替え
            Play.Visible = true;
            Stop.Visible = false;
            //タイマー停止
            m_AreaTimer.Dispose();
        }

        /*--------------------------------------------------------------------------------
         * 指定した日付・都道府県のコロナ感染者を表示
         *--------------------------------------------------------------------------------*/
        private void NihonMethod(object sender, EventArgs e)
        {
            m_pplayer.execute("Abort B");
            m_pplayer.execute("Clear B");

            //押されたボタンの名前とIDを記憶
            string strSenderName = ((Button)sender).Name;
            int nPrefectureNumber = int.Parse(strSenderName.Substring(2));
            Console.WriteLine(nPrefectureNumber);

            // 押されたボタンの地方を取得
            int nLocalID = Localjudge(nPrefectureNumber);
            //Console.WriteLine(Localid);
 
            //ひとつ前に押されたボタンの名前が保存されているとき
            if (m_strOldButtonName != "")
            {//一つ前に押したボタンのデザインを戻す処理
                this.ResetButtonDesign();
            }

            // 今押されたボタンを一つ前のボタンとして記憶
            m_strOldButtonName = strSenderName;
            m_OldButtonColor = ((Button)sender).BackColor;
            
            Control control = null;

            if (m_bCoronaFlag)
            {//コロナ表示
                //押したボタンの色変更
                ((Button)sender).BackColor = m_jaryLocalColors[1][nLocalID];
                ((Button)sender).UseVisualStyleBackColor = true;

                m_pplayer.execute("Set V0 " + nPrefectureNumber);
                Console.WriteLine(strSenderName);
                Console.WriteLine(strSenderName.Substring(2));
                m_pplayer.execute("Set V1 '" + m_strSelectDate + "'");
                m_pplayer.execute("Play '日本地図'");

                //TextBoxに都道府県名を表示
                var Data = m_pplayer.getTableData($"T( 'コロナの情報' ) R( '{m_strSelectDate}' ) C( {nPrefectureNumber.ToString()} )");
                var Name = m_pplayer.getTableData($"T( '都道府県情報' ) R( {nPrefectureNumber.ToString()} ) C( 'name' )");
                textBox1.AppendText($"《コロナ》{m_strSelectDate}　{FormatPrefetureName(Name)} : {Data}人\r\n");
            }
            else
            {//天気予報表示
                // 押した都道府県の地方に属する都道府県IDを取得してボタンのデザインを変更.
                int[] aryLocalPrefectureIDs = m_jaryLocalPrefectureIDs[nLocalID];
                foreach (int nPrefectureID in aryLocalPrefectureIDs)
                {
                    control = this.Controls["id" + nPrefectureID.ToString()];
                    ((Button)control).FlatAppearance.BorderColor = m_jaryLocalColors[1][nLocalID];
                    ((Button)control).FlatAppearance.BorderSize = 7;
                    ((Button)sender).UseVisualStyleBackColor = true;
                }
                m_pplayer.execute($"Play '{ m_jaryScnNames[1][nLocalID + 1] }'");
                
                // TextBoxに都道府県名を表示.
                DispData(nLocalID + 1);
            }

            // Takeを別スレッドで実行
            m_TakeThread = new System.Threading.Thread(new System.Threading.ThreadStart(TakeThread));
            m_TakeThread.Start();
        }

        /*--------------------------------------------------------------------------------
         * オープンデータ保存ディレクトリ内に変更が起きた時に呼び出されるイベント.
         * スキーマを読み込みなおす.
         * 天気情報をまとめたファイルを作成する.
         *--------------------------------------------------------------------------------*/
        private void watcher_Renamed(object sender, System.IO.FileSystemEventArgs e)
        {
            if (e.Name == "新規陽性者数tmp.csv")
            {
                // 新規陽性者数.csvが更新された時の処理
                try
                {
                    // 別exeファイルを実行.
                    m_procFormatCsvExe.Start();
                }
                catch(Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
            else
            {
                // 天気情報の更新処理
                // 全ての天気に関するファイルが更新されたら、まとめる処理を実行.
                switch (e.Name)
                {
                    case "最高気温.csv":
                        m_WeatherCsvFlag |= 0x01;
                        break;
                    case "最低気温.csv":
                        m_WeatherCsvFlag |= 0x02;
                        break;
                    case "日降水量.csv":
                        m_WeatherCsvFlag |= 0x04;
                        break;
                    case "最大風速.csv":
                        m_WeatherCsvFlag |= 0x08;
                        break;
                    default:
                        break;
                }

                if (m_WeatherCsvFlag == 0x0f)
                {
                    // 4種類の天気情報すべてが更新された時の処理
                    try
                    {
                        // 別exeファイル（天気情報をまとめるプログラム）を実行.
                        m_procGatherCsvExe.Start();

                        m_WeatherCsvFlag = 0x00;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
                }
            }
        }


        /*--------------------------------------------------------------------------------
         *  以下サブメソッド（コントロールに直接呼ばれるメソッドは以上）
         * --------------------------------------------------------------------------------*/


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

        /*--------------------------------------------------------------------------------
         * スキーマの読み込み.
         *--------------------------------------------------------------------------------*/
        private void LoadScheme()
        {
            string strSchemePath = m_strSceneDir.Replace(@"\", @"\\");
            m_pplayer.execute("Load '" + strSchemePath + @"Scn\\TeamDevelopment.scm'");
        }

        private void ReEnableButtons()
        {
            string strCsvPath = $@"{m_strSceneDir}\Data\";
            if (System.IO.File.Exists($"{strCsvPath}新規陽性者数.csv") && System.IO.File.Exists($"{strCsvPath}天気情報.csv") && m_bCsvExistFlag)
            {
                if (textBox1.InvokeRequired)
                {
                    textBox1.Invoke((MethodInvoker)delegate
                    {
                        textBox1.AppendText("データダウンロード完了\r\n");
                        textBox1.AppendText("《コロナ》\r\n");
                    });
                }
                else
                {
                    textBox1.AppendText("データダウンロード完了\r\n");
                    textBox1.AppendText("《コロナ》\r\n");
                }
                
                if(Play.InvokeRequired)
                {
                    Play.Invoke((MethodInvoker)delegate
                    {
                        Play.Enabled = true;
                    });
                }
                else
                {
                    Play.Enabled = true;
                }

                if (Corona.InvokeRequired)
                {
                    Corona.Invoke((MethodInvoker)delegate
                    {
                        Corona.Enabled = true;
                    });
                }
                else
                {
                    Corona.Enabled = true;
                }
                if (Weather.InvokeRequired)
                {
                    Weather.Invoke((MethodInvoker)delegate
                    {
                        Weather.Enabled = true;
                    });
                }
                else
                {
                    Weather.Enabled = true;
                }

                foreach (int[] aryPrefectureIDs in m_jaryLocalPrefectureIDs)
                {
                    foreach (int nPrefectureID in aryPrefectureIDs)
                    {
                        Control control = this.Controls[$"id{nPrefectureID}"];
                        Button button = ((Button)control);
                        if (button.InvokeRequired)
                        {
                            button.Invoke((MethodInvoker)delegate
                            {
                                button.Enabled = true;
                                int nLocalID = Localjudge(nPrefectureID);
                                button.BackColor = m_jaryLocalColors[0][nLocalID];
                            });
                        }
                        else
                        {
                            button.Enabled = true;
                            int nLocalID = Localjudge(nPrefectureID);
                            button.BackColor = m_jaryLocalColors[0][nLocalID];
                        }
                    }
                }

                if (m_bCoronaFlag)
                {
                    if (Corona.InvokeRequired)
                    {
                        Corona.Invoke((MethodInvoker)delegate
                        {
                            Corona.BackgroundImage = Properties.Resources.covid19button_put;
                        });
                    }
                    else
                    {
                        Corona.BackgroundImage = Properties.Resources.covid19button_put;
                    }

                    if (Weather.InvokeRequired)
                    {
                        Weather.Invoke((MethodInvoker)delegate
                        {
                            Weather.BackgroundImage = Properties.Resources.weatherbutton;
                        });
                    }
                    else
                    {
                        Weather.BackgroundImage = Properties.Resources.weatherbutton;
                    }
                }

                if (MonthCalendar.InvokeRequired)
                {
                    MonthCalendar.Invoke((MethodInvoker)delegate
                    {
                        MonthCalendar.Visible = true;
                        MonthCalendar.SelectionStart = MonthCalendar.MaxDate;
                    });
                }
                else
                {
                    MonthCalendar.Visible = true;
                    MonthCalendar.SelectionStart = MonthCalendar.MaxDate;
                }

                m_bCsvExistFlag = false;
            }
        }

        /*--------------------------------------------------------------------------------
         * 新規陽性者数.csvを更新した後に実行されるメソッド.
         *--------------------------------------------------------------------------------*/
        private void UpdatedCovidData(object sender, EventArgs e)
        {
            ReEnableButtons();
            string strCovidCsvPath = $@"{m_strSceneDir}\Data\新規陽性者数.csv";
            if (MonthCalendar.InvokeRequired)
            {
                MonthCalendar.Invoke((MethodInvoker)delegate
                {
                    MonthCalendar.MaxDate = getLatestDate(strCovidCsvPath);
                    MonthCalendar.SelectionStart = MonthCalendar.MaxDate;
                });
            }
            else
            {
                MonthCalendar.MaxDate = getLatestDate(strCovidCsvPath);
                MonthCalendar.SelectionStart = MonthCalendar.MaxDate;
            }
            LoadScheme();
        }

        /*--------------------------------------------------------------------------------
         * 天気情報.csvを更新した後に実行されるメソッド.
         *--------------------------------------------------------------------------------*/
        private void UpdatedWeatherCsv(object sender, EventArgs e)
        {
            ReEnableButtons();
            LoadScheme();
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

        /*--------------------------------------------------------------------------------
         * 受け取った都道府県名の文字幅を整えた文字列を返すメソッド.
         *--------------------------------------------------------------------------------*/
        private string FormatPrefetureName(string strName)
        {
            string strRet = "";
            if (strName == "北海道")
            {
                strRet = " " + strName + "　";
            }
            else if (strName == "東京")
            {
                strRet = " " + strName + "都" + "　";
            }
            else if (strName == "大阪" || strName == "京都")
            {
                strRet = " " + strName + "府" + "　";
            }
            else if (strName.Length == 3)
            {
                strRet = strName + "県";
            }
            else
            {
                strRet = " " + strName + "県" + "　";
            }
            return strRet;
        }

        /*--------------------------------------------------------------------------------
         * 受け取った県庁所在地名の文字幅を整えた文字列を返すメソッド.
         *--------------------------------------------------------------------------------*/
        private string FormatPrefCapital(string strName)
        {
            string strRet = "";
            if (strName == "東京")
            {
                strRet = "　東　京　";
            }
            else if (strName.Length == 4)
            {
                strRet = strName + "市";
            }
            else if (strName.Length == 3)
            {
                strRet = strName + "市";
            }
            else if (strName.Length == 1)
            {
                strRet = $"　{strName}　市　";
            }
            else
            {
                strRet = $" {strName}市　";
            }
            return strRet;
        }

        /*--------------------------------------------------------------------------------
         * 都道府県番号からその地域の盆号を返すメソッド.
         * 北海道：０
         * 　東北：１
         * 　関東：２
         * 　中部：３
         * 　近畿：４
         * 　中国：５
         * 　四国：６
         * 　九州：７
         *--------------------------------------------------------------------------------*/
        private int Localjudge(int nPrefectureID)
        {
            int nretLocalID = -1;

            if (nPrefectureID == 2)
            {
                nretLocalID = 0;
            }
            else if (3 <= nPrefectureID && nPrefectureID <= 8)
            {
                nretLocalID = 1;
            }
            else if (9 <= nPrefectureID && nPrefectureID <= 15)
            {
                nretLocalID = 2;
            }
            else if (16 <= nPrefectureID && nPrefectureID <= 24)
            {
                nretLocalID = 3;
            }
            else if (25 <= nPrefectureID && nPrefectureID <= 31)
            {
                nretLocalID = 4;
            }
            else if (32 <= nPrefectureID && nPrefectureID <= 36)
            {
                nretLocalID = 5;
            }
            else if (37 <= nPrefectureID && nPrefectureID <= 40)
            {
                nretLocalID = 6;
            }
            else if (41 <= nPrefectureID && nPrefectureID <= 48)
            {
                nretLocalID = 7;
            }
            return nretLocalID;
        }

        /*--------------------------------------------------------------------------------
         *  textBox1に送出中のシーンが参照しているデータを表示するメソッド.
         *--------------------------------------------------------------------------------*/
        private void DispData(int nLocalID)
        {
            try
            {
                string strScnName = "";
                if (m_bCoronaFlag)
                {
                    // シーン名と日付を取得・表示
                    strScnName = m_jaryScnNames[0][nLocalID];
                    var Date = m_pplayer.getTableData($"T( 'コロナの情報' ) C( '日付' ) ");
                    textBox1.Invoke((MethodInvoker)delegate
                    {
                        textBox1.AppendText($"'{strScnName}' ({Date})\r\n");
                    });

                    // 同地方の各都道府県名と新規陽性者数を取得・表示
                    int[] aryPrefectureIDs = m_jaryLocalPrefectureIDs[nLocalID];
                    foreach (int PrefectureID in aryPrefectureIDs)
                    {
                        var Name = m_pplayer.getTableData($"T( '都道府県情報' ) R( {PrefectureID} ) C( 'name' )");
                        var Data = m_pplayer.getTableData($"T( 'コロナの情報' ) C( {PrefectureID} )");
                        textBox1.Invoke((MethodInvoker)delegate
                        {
                            textBox1.AppendText($"　・{FormatPrefetureName(Name)} : {Data}人\r\n");
                        });
                    }
                }
                else
                {
                    // シーン名と日付を取得・表示
                    strScnName = m_jaryScnNames[1][nLocalID];
                    var Year = m_pplayer.getTableData("T( '天気情報' ) C( '年' )");
                    var Month = m_pplayer.getTableData("T( '天気情報' ) C( '月' )");
                    var Date = m_pplayer.getTableData("T( '天気情報' ) C( '日' )");
                    var Hour = m_pplayer.getTableData("T( '天気情報' ) C( '時' )");
                    var Minute = m_pplayer.getTableData("T( '天気情報' ) C( '分' )");
                    string strDate = $"{Year}/{Month}/{Date} {Hour}:{Minute}0";
                    if (textBox1.InvokeRequired)
                    {
                        textBox1.Invoke((MethodInvoker)delegate
                        {
                            textBox1.AppendText($"'{strScnName}' ({strDate})\r\n");
                        });
                    }
                    else
                    {
                        textBox1.AppendText($"{strScnName} ({strDate})\r\n");
                    }

                    // 送出中のシーンに表示されている観測所番号リストを取得
                    int[] arySpotIDs = m_jarySpotIDs[nLocalID];
                    foreach (int nSpotID in arySpotIDs)
                    {
                        // 観測所番号から名前と各情報を取得・表示
                        var name = m_pplayer.getTableData($"T( '天気情報' ) R( {nSpotID} ) C( '地点' )");
                        var HighTemp = m_pplayer.getTableData($"T( '天気情報' ) R( {nSpotID.ToString()} ) C( '今日の最高気温' )");
                        var LowTemp = m_pplayer.getTableData($"T( '天気情報' ) R( { nSpotID.ToString()} ) C('今日の最低気温')");
                        var Rain = m_pplayer.getTableData($"T( '天気情報' ) R( {nSpotID.ToString()} ) C( '現在の降水量' )");
                        var Wind = m_pplayer.getTableData($"T( '天気情報' ) R( {nSpotID.ToString()} ) C( '今日の最大風速' )");
                        if (textBox1.InvokeRequired)
                        {
                            textBox1.Invoke((MethodInvoker)delegate
                            {
                                textBox1.AppendText($"　・{FormatPrefCapital(name)}　{HighTemp}℃, {LowTemp}℃, {Rain} mm, {Wind} m/s\r\n");
                            });
                        }
                        else
                        {
                            textBox1.AppendText($"　・{FormatPrefCapital(name)}　{HighTemp}℃, {LowTemp}℃, {Rain} mm, {Wind} m/s\r\n");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (textBox1.InvokeRequired)
                {
                    textBox1.Invoke((MethodInvoker)delegate
                    {
                        textBox1.AppendText(ex.Message);
                    });
                }
                else
                {
                    textBox1.AppendText(ex.Message);
                }
            }
        }

        /*--------------------------------------------------------------------------------
         * ボタンのデザインを元に戻す処理.
         *--------------------------------------------------------------------------------*/
        private void ResetButtonDesign()
        {
            //ひとつ前に押されたボタンの都道府県番号と地方番号を取得
            int nOldButtonNumber = int.Parse(m_strOldButtonName.Substring(2));
            int nOldLocalID = Localjudge(nOldButtonNumber);
            Control OldButton = null;

            //押されたボタンを元に戻す
            // 押されたボタンの地方の各都道府県のID配列を取得
            int[] aryOldLocalPrefectureIDs = m_jaryLocalPrefectureIDs[nOldLocalID];
            foreach (int nPrefectureID in aryOldLocalPrefectureIDs)
            {
                OldButton = this.Controls["id" + nPrefectureID.ToString()];
                ((Button)OldButton).BackColor = m_OldButtonColor;
                ((Button)OldButton).FlatAppearance.BorderColor = m_OldButtonBorderColor;
                ((Button)OldButton).FlatAppearance.BorderSize = (int)m_OldButtonStyle;
                ((Button)OldButton).UseVisualStyleBackColor = true;
            }
        }
    }
}