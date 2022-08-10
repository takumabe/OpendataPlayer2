using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskScheduler;
using Microsoft.Win32;

namespace OpendataDownLoader
{
    public class OpendataDownLoader
    {
        /* 変数 */
        private ITaskService m_TaskService = null;
        private ITaskFolder m_RootFolder = null;
        private string m_TaskSchedulerPath = "";    // タスクスケジューラライブラリのパス名
        private string m_XmlPath = "";


        /*--------------------------------------------------------------------------------
         * コンストラクタ.
         * タスクサービスクラスの初期化.
         * タスクスケジューラライブラリのフォルダ設定.
         * 各パス情報設定.
         * オープンデータの保存先ディレクトリ作成.
         *--------------------------------------------------------------------------------*/
        public OpendataDownLoader()
        {
            try
            {
                // ITaskServiceを初期化
                this.m_TaskService = new TaskScheduler.TaskScheduler();
                this.m_TaskService.Connect(null, null, null, null);

                // ITaskFolderを初期化
                this.m_RootFolder = this.m_TaskService.GetFolder(@"\");

                // パス情報設定
                this.m_TaskSchedulerPath = @"\OpenDataPlayer\";
                this.m_XmlPath = $@"{AppDomain.CurrentDomain.BaseDirectory}";
                this.m_XmlPath = this.m_XmlPath.Substring(0, this.m_XmlPath.IndexOf(@"\bin"));
                this.m_XmlPath = this.m_XmlPath.Substring(0, this.m_XmlPath.LastIndexOf(@"\")) + @"\OpendataDownLoader\Xml";

                // Xmlテンプレートがなければ例外処理
                string strXmlTemplateDir = this.m_XmlPath + @"\Template";
                if (!System.IO.Directory.Exists(strXmlTemplateDir))
                {
                    System.IO.Directory.CreateDirectory(strXmlTemplateDir);
                    throw new Exception(strXmlTemplateDir + "にXMLファイルのテンプレートが存在しません。\n");
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        ~OpendataDownLoader()
        {
            try
            {
                this.deleteTask();
            }
            catch
            {
                return;
            }
            finally
            {
                if (this.m_TaskService != null)
                {
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(this.m_TaskService);
                }
                if (this.m_RootFolder != null)
                {
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(this.m_RootFolder);
                }
            }
        }


        /*--------------------------------------------------------------------------------
         * オープンデータダウンロードvbs(bat)の生成関数.
         * url:ダウンロード元URL
         * csvpath：ダウンロード先パス
         * csvfilename：ダウンロード名
         * taskname：生成するtask/bat/vbsファイル名
         *--------------------------------------------------------------------------------*/
        public void createTaskCommand(string strUrl, string strCsvPath, string strCommandFilePath, string strCommandFileName)
        {
            // オープンデータ保存先ディレクトリがなければ作成
            string strDataPath = strCsvPath.Substring(0, strCsvPath.LastIndexOf(@"\"));
            if (!System.IO.Directory.Exists(strDataPath))
            {
                System.IO.Directory.CreateDirectory(strDataPath);
            }

            if (strCommandFilePath.EndsWith(@"\"))
            {
                strCommandFilePath = strCommandFilePath.Substring(0, strCommandFilePath.LastIndexOf(@"\"));
            }

            if (!System.IO.Directory.Exists(strCommandFilePath))
            {
                System.IO.Directory.CreateDirectory(strCommandFilePath);
            }

            string strBatFullPath = strCommandFilePath + @"\" + strCommandFileName + ".bat";
            string strVbsFullPath = strCommandFilePath + @"\" + strCommandFileName + ".vbs";

            System.IO.File.WriteAllText(strBatFullPath, "@echo off" + Environment.NewLine, System.Text.Encoding.GetEncoding("shift_jis"));
            System.IO.File.AppendAllText(strBatFullPath, "bitsadmin /transfer " + strCommandFileName + "_OpendataDownLoader " + strUrl + " " + strCsvPath, System.Text.Encoding.GetEncoding("shift_jis"));

            System.IO.File.WriteAllText(strVbsFullPath, "Set ws = CreateObject(\"Wscript.Shell\")" + Environment.NewLine, System.Text.Encoding.GetEncoding("shift_jis"));
            System.IO.File.AppendAllText(strVbsFullPath, "ws.run \"cmd /c " + strBatFullPath + "\", vbHide" + Environment.NewLine, System.Text.Encoding.GetEncoding("shift_jis"));
        }

        private void addFormatBat(string strBatPath)
        {
            string strFormatExePath = $@"{AppDomain.CurrentDomain.BaseDirectory}";
            strFormatExePath = strFormatExePath.Substring(0, strFormatExePath.IndexOf(@"\bin"));
            strFormatExePath = strFormatExePath.Substring(0, strFormatExePath.LastIndexOf(@"\")) + @"\OpendataDownLoader\FormatCovidCsv\FormatCovidCsv\bin\Debug\FotmatCovidCsv.exe";
            System.IO.File.AppendAllText(strBatPath, " && " + strFormatExePath, System.Text.Encoding.GetEncoding("shift-jis"));
        }

        /*--------------------------------------------------------------------------------
         * タスクスケジューラ登録用情報のxmlファイルに書き込むユーザーのSID取得関数.
         * Microsoft.Win32を参照するためx64でビルド.
         *--------------------------------------------------------------------------------*/
        private string getSID()
        {
            string ret = "";
            // 現在ログイン中のユーザ名取得
            string currentUserName = System.Security.Principal.WindowsIdentity.GetCurrent().Name.ToString();
            Microsoft.Win32.RegistryKey regDir = Microsoft.Win32.Registry.LocalMachine;
            using (Microsoft.Win32.RegistryKey regKey = regDir.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\LogonUI\SessionData", true))
            {// レジストリキーから現在ログイン中のユーザと一致するユーザのSIDを取得
                if (regKey != null)
                {
                    string[] valueNames = regKey.GetSubKeyNames();
                    for (int i = 0; i < valueNames.Length; i++)
                    {
                        using (Microsoft.Win32.RegistryKey key = regKey.OpenSubKey(valueNames[i], true))
                        {
                            string[] names = key.GetValueNames();
                            for (int j = 0; j < names.Length; j++)
                            {
                                if (names[j] == "LoggedOnSAMUser")
                                {
                                    if (key.GetValue(names[j]).ToString() == currentUserName)
                                    {
                                        ret = key.GetValue("LoggedOnUserSID").ToString();
                                    }
                                }
                            }
                        }

                    }
                }
            }
            return ret;
        }

        /*--------------------------------------------------------------------------------
         * xml作成関数.(実行間隔＝日にち指定)
         * strTaskName：タスクスケジューラに登録するタスク名.
         * strStart：タスク開始日時.
         * strEnd：タスク終了日時.
         * srtCommandPath：実行するプログラムのパス.
         * nInterval：実行間隔.
         *--------------------------------------------------------------------------------*/
        public System.Xml.XmlDocument createXmlDay(string strSchedulerPath, string strStart, string strEnd, string strCommandPath, int nInterval)
        {
            System.Xml.XmlDocument xmlDocument = new System.Xml.XmlDocument();

            // スケジューラライブラリのパスからタスク名取得
            string strTaskName = strSchedulerPath;
            if (strTaskName.Contains(@"\"))
            {
                strTaskName = strTaskName.Substring(strTaskName.LastIndexOf(@"\") + 1);

            }

            try
            {
                // テンプレートファイルを開く
                xmlDocument.Load(this.m_XmlPath + @"\Template\DayTemplate.xml");

                // 実行間隔の設定
                var daysinterval = xmlDocument.GetElementsByTagName("DaysInterval")[0];
                daysinterval.InnerText = nInterval.ToString();
            }
            catch (Exception ex)
            {
                throw ex;
            }

            // 登録日時の設定
            string now = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            var date = xmlDocument.GetElementsByTagName("Date")[0];
            date.InnerText = now;

            // 登録者の設定
            var author = xmlDocument.GetElementsByTagName("Author")[0];
            author.InnerText = $@"{Environment.UserDomainName}\{Environment.UserName}";

            // 説明文の設定
            var description = xmlDocument.GetElementsByTagName("Description")[0];
            description.InnerText = "これはOpendataDownLoaderによって登録された\"" + strTaskName + "\"タスクです。";

            // タスクスケジューラライブラリのパス情報設定
            var uri = xmlDocument.GetElementsByTagName("URI")[0];
            uri.InnerText = this.m_TaskSchedulerPath + strSchedulerPath;

            // 開始・終了日時の設定
            var startb = xmlDocument.GetElementsByTagName("StartBoundary")[0];
            startb.InnerText = strStart;
            var endb = xmlDocument.GetElementsByTagName("EndBoundary")[0];
            endb.InnerText = strEnd;

            // sidの設定
            string SID = this.getSID();
            var userid = xmlDocument.GetElementsByTagName("UserId")[0];
            userid.InnerText = SID;

            // 実行バッチのパス設定
            var command = xmlDocument.GetElementsByTagName("Command")[0];
            command.InnerText = strCommandPath;

            try
            {
                // 作成したxmlファイルを保存
                xmlDocument.Save(this.m_XmlPath + @"\" + strTaskName.Replace(@"\", "_") + ".xml");
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return xmlDocument;
        }

        /*--------------------------------------------------------------------------------
         * xml作成関数.(実行間隔＝時間指定)
         * strTaskName：タスクスケジューラに登録するタスク名.
         * strStart：タスク開始日時.
         * strEnd：タスク終了日時.
         * srtCommandPath：実行するプログラムのパス.
         * strInterval：実行間隔.(ISO8601 duration specification（ISO8601 期間仕様）に従う)
         * (ex："PT1H"＝1時間毎, "PT30M"＝30分毎、"PT10S"＝10秒毎)
         *--------------------------------------------------------------------------------*/
        public System.Xml.XmlDocument createXmlTime(string strSchedulerPath, string strStart, string strEnd, string strCommandPath, string strInterval)
        {
            System.Xml.XmlDocument xmlDocument = new System.Xml.XmlDocument();

            string strTaskName = strSchedulerPath;
            if (strTaskName.Contains(@"\"))
            {
                strTaskName = strTaskName.Substring(strTaskName.LastIndexOf(@"\") + 1);

            }

            try
            {
                xmlDocument.Load(this.m_XmlPath + @"\Template\TimeTemplate.xml");

                // 実行間隔の設定
                var daysinterval = xmlDocument.GetElementsByTagName("Interval")[0];
                daysinterval.InnerText = strInterval;

                // 登録日時の設定
                string now = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
                var date = xmlDocument.GetElementsByTagName("Date")[0];
                date.InnerText = now;

                // 登録者の設定
                var author = xmlDocument.GetElementsByTagName("Author")[0];
                author.InnerText = $@"{Environment.UserDomainName}\{Environment.UserName}";

                // 説明文の設定
                var description = xmlDocument.GetElementsByTagName("Description")[0];
                description.InnerText = "これはOpendataDownLoaderによって登録された\"" + strTaskName + "\"タスクです。";

                // タスクスケジューラライブラリのパス情報設定
                var uri = xmlDocument.GetElementsByTagName("URI")[0];
                uri.InnerText = this.m_TaskSchedulerPath + strSchedulerPath;

                // 開始・終了日時の設定
                var startb = xmlDocument.GetElementsByTagName("StartBoundary")[0];
                startb.InnerText = strStart;
                var endb = xmlDocument.GetElementsByTagName("EndBoundary")[0];
                endb.InnerText = strEnd;

                // sidの設定
                string SID = this.getSID();
                var userid = xmlDocument.GetElementsByTagName("UserId")[0];
                userid.InnerText = SID;

                // 実行バッチのパス設定
                var command = xmlDocument.GetElementsByTagName("Command")[0];
                command.InnerText = strCommandPath;

                try
                {
                    // 作成したxmlファイルを保存
                    xmlDocument.Save(this.m_XmlPath + @"\" + strTaskName.Replace(@"\", "_") + ".xml");
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return xmlDocument;
        }

        /*--------------------------------------------------------------------------------
         * 指定したxmlの設定をもとにタスクを登録.
         * strSchedulerPath：登録するタスクスケジューラライブラリのパス.
         * xmlDocument：登録設定が保存されたxmlドキュメント.
         *--------------------------------------------------------------------------------*/
        public string registerTaskByXml(System.Xml.XmlDocument xmlDocument)
        {
            bool bResult = false;

            // スケジューラライブラリのパスをXmlファイルから取得
            string strSchedulerPath = xmlDocument.GetElementsByTagName("URI")[0].InnerText;

            try
            {
                // 登録
                this.m_RootFolder.RegisterTask(
                    strSchedulerPath,
                    xmlDocument.InnerXml,
                    (int)_TASK_CREATION.TASK_CREATE_OR_UPDATE,
                    null,
                    null,
                    _TASK_LOGON_TYPE.TASK_LOGON_NONE,
                    null
                    );
            }
            catch (Exception ex)
            {
                throw ex;
            }


            try
            {
                // 作成したタスクが存在するかチェック
                IRegisteredTask registeredTask = m_RootFolder.GetTask(strSchedulerPath);
                string taskName = strSchedulerPath.Substring(strSchedulerPath.LastIndexOf(@"\") + 1);
                if (registeredTask.Name == taskName)
                {
                    // 初回登録時実行
                    registeredTask.Run(null);
                    bResult = true;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

            if (!bResult)
            {
                strSchedulerPath = "";
            }

            return strSchedulerPath;
        }

        /*--------------------------------------------------------------------------------
         * 指定したスケジューラライブラリパス内のタスクとフォルダをすべて削除する再帰関数.
         * strSchedulerPath：削除するスケジューラライブラリパス.
         *--------------------------------------------------------------------------------*/
        private void serachFolder(string strSchedulerPath)
        {
            this.m_RootFolder = this.m_TaskService.GetFolder(strSchedulerPath);
            ITaskFolderCollection TaskFolders = m_RootFolder.GetFolders(0);
            foreach (ITaskFolder TaskFolder in TaskFolders)
            {
                Console.WriteLine(TaskFolder.Path);
                serachFolder(TaskFolder.Path);
            }
            IRegisteredTaskCollection RegisterdTasks = m_RootFolder.GetTasks(0);
            foreach (IRegisteredTask RegisterdTask in RegisterdTasks)
            {
                Console.WriteLine(RegisterdTask.Path);
                m_RootFolder.DeleteTask(RegisterdTask.Name, 0);
            }
            return;
        }

        /*--------------------------------------------------------------------------------
         * このクラスで登録したタスクスケジューラライブラリのフォルダを削除.
         *--------------------------------------------------------------------------------*/
        public void deleteTask()
        {
            try
            {
                string strSchedulerPath = this.m_TaskSchedulerPath.Substring(0, this.m_TaskSchedulerPath.LastIndexOf(@"\"));
                serachFolder(strSchedulerPath);

                try
                {
                    this.m_RootFolder = this.m_TaskService.GetFolder(@"\");
                    this.m_RootFolder.DeleteFolder(strSchedulerPath, 0);
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /*--------------------------------------------------------------------------------
         * OpendataPlayer用セットアップメソッド.
         * コロナ新規陽性者数のオープンデータ自動取得プログラム
         * 最高気温・最低気温・降水量・最大風速のオープンデータ自動取得プログラム
         * をタスクスケジューラに登録.
         *--------------------------------------------------------------------------------*/
        public bool setupOpendataPlayer()
        {
            bool bResult = true;

            string strCommandDir = $"{AppDomain.CurrentDomain.BaseDirectory}";
            strCommandDir = strCommandDir.Substring(0, strCommandDir.IndexOf(@"\bin"));
            strCommandDir = strCommandDir.Substring(0, strCommandDir.LastIndexOf(@"\")) + @"\OpendataDownLoader\TaskCommand\";
            string strCsvPath = $"{AppDomain.CurrentDomain.BaseDirectory}";
            strCsvPath = strCsvPath.Substring(0, strCsvPath.IndexOf(@"\bin"));
            strCsvPath = strCsvPath.Substring(0, strCsvPath.LastIndexOf(@"\")) + @"\シーン\Data\";
            string strStart = DateTime.Now.ToString("yyyy-MM-ddT") + "00:00:00";
            string strEnd = "2100-01-01T00:00:00";

            string URLCovid = "https://covid19.mhlw.go.jp/public/opendata/newly_confirmed_cases_daily.csv";
            string CsvPathCovid = strCsvPath + @"\新規陽性者数tmp.csv";
            string CommandFileNameCovid = "covid-19";
            string strBatPathCovid = strCommandDir + CommandFileNameCovid + ".bat";
            string SchedulerPathCovid = "covid-19";

            string URLHTemp = "https://www.data.jma.go.jp/obd/stats/data/mdrr/tem_rct/alltable/mxtemsadext00_rct.csv";
            string CsvPathHTemp = strCsvPath + @"\最高気温.csv";
            string CommandFileNameHTemp = "HighTemp";
            string SchedulerPathHTemp = "HighTemp";

            string URLLTemp = "https://www.data.jma.go.jp/obd/stats/data/mdrr/tem_rct/alltable/mntemsadext00_rct.csv";
            string CsvPathLTemp = strCsvPath + @"\最低気温.csv";
            string CommandFileNameLTemp = "LowTemp";
            string SchedulerPathLTemp = "LowTemp";

            string URLRain = "https://www.data.jma.go.jp/obd/stats/data/mdrr/pre_rct/alltable/predaily00_rct.csv";
            string CsvPathRain = strCsvPath + @"\日降水量.csv";
            string CommandFileNameRain = "Rain";
            string SchedulerPathRain = "Rain";

            string URLWind = "https://www.data.jma.go.jp/obd/stats/data/mdrr/wind_rct/alltable/mxwsp00_rct.csv";
            string CsvPathWind = strCsvPath + @"\最大風速.csv";
            string CommandFileNameWind = "Wind";
            string SchedulerPathWind = "Wind";

            try
            {
                this.createTaskCommand(URLCovid, CsvPathCovid, strCommandDir, CommandFileNameCovid);
                this.addFormatBat(strBatPathCovid);
                System.Xml.XmlDocument covidXml = this.createXmlTime(SchedulerPathCovid, strStart, strEnd, strCommandDir + CommandFileNameCovid + ".vbs", "PT6H");
                if (this.registerTaskByXml(covidXml) == "")
                {
                    bResult = false;
                    throw new Exception(SchedulerPathCovid + "タスクの登録に失敗\n");
                }

                this.createTaskCommand(URLHTemp, CsvPathHTemp, strCommandDir, CommandFileNameHTemp);
                System.Xml.XmlDocument HTempXml = this.createXmlTime(SchedulerPathHTemp, strStart, strEnd, strCommandDir + CommandFileNameHTemp + ".vbs", "PT1H");
                if (this.registerTaskByXml(HTempXml) == "")
                {
                    bResult = false;
                    throw new Exception(SchedulerPathHTemp + "タスクの登録に失敗\n");
                }

                this.createTaskCommand(URLLTemp, CsvPathLTemp, strCommandDir, CommandFileNameLTemp);
                System.Xml.XmlDocument LTempXml = this.createXmlTime(SchedulerPathLTemp, strStart, strEnd, strCommandDir + CommandFileNameLTemp + ".vbs", "PT1H");
                if (this.registerTaskByXml(LTempXml) == "")
                {
                    bResult = false;
                    throw new Exception(SchedulerPathLTemp + "タスクの登録に失敗\n");
                }

                this.createTaskCommand(URLRain, CsvPathRain, strCommandDir, CommandFileNameRain);
                System.Xml.XmlDocument RainXml = this.createXmlTime(SchedulerPathRain, strStart, strEnd, strCommandDir + CommandFileNameRain + ".vbs", "PT1H");
                if (this.registerTaskByXml(RainXml) == "")
                {
                    bResult = false;
                    throw new Exception(SchedulerPathRain + "タスクの登録に失敗\n");
                }

                this.createTaskCommand(URLWind, CsvPathWind, strCommandDir, CommandFileNameWind);
                System.Xml.XmlDocument WindXml = this.createXmlTime(SchedulerPathWind, strStart, strEnd, strCommandDir + CommandFileNameWind + ".vbs", "PT1H");
                if (this.registerTaskByXml(WindXml) == "")
                {
                    bResult = false;
                    throw new Exception(SchedulerPathWind + "タスクの登録に失敗\n");
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return bResult;
        }
    }
}
