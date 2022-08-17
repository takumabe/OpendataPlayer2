using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Excel = Microsoft.Office.Interop.Excel;
using System.Runtime.InteropServices;

namespace GatherWeatherCsv
{
    class Program
    {
        static void Main(string[] args)
        {
            Program obj = new Program();

            System.Diagnostics.Debug.WriteLine("天気情報.csv生成タスク実行開始");

            int[] aryRegionID = new int[]
            {
                // 各都道府県の県庁所在地の観測所番号リスト.
                14163, 19432, 31312, 32402, 33431, 34392, 35426, 36127, 40201, 41277,
                42251, 43241, 44132, 45212, 46106, 48156, 49142, 50331, 51106, 52586,
                53133, 54232, 55102, 56227, 57066, 60216, 61286, 62078, 63518, 64036,
                65042, 66408, 67437, 68132, 69122, 71106, 72086, 73166, 74181, 81286,
                82182, 83216, 84496, 85142, 86141, 87376, 88317, 91197
            };
            bool bFileExistFlag = false;
            string strDataDir = $"{AppDomain.CurrentDomain.BaseDirectory}";
            strDataDir = strDataDir.Substring(0, strDataDir.LastIndexOf("OpendataDownLoader")) + @"シーン\Data\";

            if(!System.IO.File.Exists(strDataDir + "天気情報.csv"))
            {
                System.Diagnostics.Debug.WriteLine("天気情報.csvを作成します。");
                using (System.IO.StreamWriter sw = new System.IO.StreamWriter(strDataDir + "天気情報.csv", false, System.Text.Encoding.GetEncoding("shift-jis")))
                {
                    sw.Write("#観測所番号,地点,今日の最高気温(℃),今日の最低気温(℃),現在の降水量(mm),今日の最大風速(m/s),年,月,日,時,分");
                }
                bFileExistFlag = true;
            }

            Microsoft.Office.Interop.Excel.Application xlApp = null;
            Excel.Workbooks Workbooks = null;

            Excel.Workbook wbWeather = null;
            Excel.Workbook wbHigh = null;
            Excel.Workbook wbLow = null;
            Excel.Workbook wbRain = null;
            Excel.Workbook wbWind = null;

            Excel.Sheets SrcSheets = null;
            Excel._Worksheet SrcWSheet = null;

            Excel.Range SrcUsedRange = null;
            Excel.Range SrcRowsRange = null;

            Excel.Range SrcRegionCell = null;
            Excel.Range SrcDataCell = null;

            Excel.Range DestCell = null;
            Excel._Worksheet DestCsv = null;

            try
            {
                xlApp = new Microsoft.Office.Interop.Excel.Application();
                xlApp.Visible = false;
                xlApp.DisplayAlerts = false;

                Workbooks = xlApp.Workbooks;

                wbWeather = Workbooks.Open(strDataDir + "天気情報.csv");
                wbHigh = Workbooks.Open(strDataDir + "最高気温.csv",
                        Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing,
                        Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing,
                        Type.Missing);
                wbLow = Workbooks.Open(strDataDir + "最低気温.csv",
                        Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing,
                        Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing,
                        Type.Missing);
                wbRain = Workbooks.Open(strDataDir + "日降水量.csv",
                        Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing,
                        Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing,
                        Type.Missing);
                wbWind = Workbooks.Open(strDataDir + "最大風速.csv",
                        Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing,
                        Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing,
                        Type.Missing);

                // 二番目に開いたワークブックから順にアクセス（一番目は書き込み先）.
                for (int wbIndex = 2; wbIndex <= Workbooks.Count; wbIndex++)
                {
                    Workbooks[wbIndex].Activate();
                    SrcSheets = Workbooks[wbIndex].Worksheets;

                    // 選択中のワークブックにあるワークシートを順にアクセス.
                    for (int wsIndex = 1; wsIndex <= SrcSheets.Count; wsIndex++)
                    {
                        // wbIndex番目のワークブックのwsIndex番目のシートを選択.
                        SrcWSheet = (Excel._Worksheet)SrcSheets[wsIndex];
                        SrcWSheet.Select();

                        // 県庁所在地のみの観測所番号リスト用インデックス.
                        int RegionIndex = 0;

                        // 有効な行数を取得.
                        SrcUsedRange = SrcWSheet.UsedRange;
                        SrcRowsRange = SrcUsedRange.Rows;

                        // 観測所番号の列（A列）を2行目（1行目は列の説明文）から順にアクセス.
                        for (int nRow = 2; nRow <= SrcRowsRange.Count; nRow++)
                        {
                            SrcRegionCell = SrcWSheet.get_Range("A" + nRow.ToString());

                            // 選択中のセルの値が県庁所在地の観測所番号リストに存在するか判定.
                            // （データは観測所番号が昇順で重複がないこととする.）
                            if (SrcRegionCell.Text == aryRegionID[RegionIndex].ToString())
                            {
                                System.Diagnostics.Debug.WriteLine("処理中...");
                                // 情報の貼り付け先csvファイルを用意（最初に開いたワークブックの一枚目のワークシート）.
                                DestCsv = (Excel._Worksheet)Workbooks[1].Worksheets[1];

                                // 2行目から48地点分を順番に貼り付ける（一行目を列の説明文とするため）.
                                int DestRow = RegionIndex + 2;

                                if (wbIndex == 2)
                                {
                                    // 一つ目のワークブックを読み取る場合のみ、観測日時情報をコピー＆ペースト
                                    SrcDataCell = SrcWSheet.get_Range("E" + nRow.ToString(), "I" + nRow.ToString());
                                    SrcDataCell.Select();
                                    SrcDataCell.Copy();

                                    DestCsv.Paste(DestCsv.get_Range("G" + DestRow.ToString()), Type.Missing);

                                    if (bFileExistFlag)
                                    {
                                        // 天気情報.csvが存在しなかった場合、観測所番号と地点名を書き込む.
                                        DestCell = DestCsv.get_Range("A" + DestRow.ToString());
                                        DestCell.Value = SrcRegionCell.Text;

                                        SrcDataCell = SrcWSheet.get_Range("C" + nRow.ToString());
                                        string strSpotName = SrcDataCell.Text;
                                        strSpotName = strSpotName.Substring(0, strSpotName.IndexOf("（"));

                                        DestCell = DestCsv.get_Range("B" + DestRow.ToString());
                                        DestCell.Value = strSpotName;
                                    }
                                }

                                // 観測所番号リストに一致した行の「各ファイルの必要なデータが保存されているJ列」のセルを選択・コピー.
                                SrcDataCell = SrcWSheet.get_Range("J" + nRow.ToString());
                                SrcDataCell.Select();
                                SrcDataCell.Copy();

                                // アクセス中のワークブックの順番によって貼り付け先の列を切り替え.
                                // C列 → D列 → E列 → F列の順.
                                // 'C'はASCIIコードで67. wbIndexは2から始まるので65を加算して文字列に変換.
                                int DestColumn_asciiCode = 65 + wbIndex;

                                // 貼り付け先を指定してペースト.
                                string strDestCell = ((char)DestColumn_asciiCode).ToString() + DestRow.ToString();
                                DestCsv.Paste(DestCsv.get_Range(strDestCell), Type.Missing);


                                if (aryRegionID.Length - 1 <= RegionIndex)
                                {
                                    // 検索する観測所番号がリストの要素数を超えたら終了.
                                    break;
                                }
                                else
                                {
                                    // それ以外の時は次の観測所番号リストへ
                                    RegionIndex++;
                                }
                            }
                        }
                    }
                }
                wbWeather.SaveAs(strDataDir + "天気情報.csv", Excel.XlFileFormat.xlCSV);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                //System.Diagnostics.Debug.WriteLine(ex.InnerException.Message);
            }
            finally
            {
                // 貼り付けた内容を保存.
                if(wbWeather != null)
                {
                    wbWeather.SaveAs(strDataDir + "天気情報.csv", Excel.XlFileFormat.xlCSV);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Save faild");
                }

                // Excelアプリの終了.
                obj.ReleaseCOM(DestCell);
                obj.ReleaseCOM(DestCsv);
                obj.ReleaseCOM(SrcDataCell);
                obj.ReleaseCOM(SrcRegionCell);

                obj.ReleaseCOM(SrcRowsRange);
                obj.ReleaseCOM(SrcUsedRange);

                obj.ReleaseCOM(SrcWSheet);
                obj.ReleaseCOM(SrcSheets);

                obj.ReleaseCOM(wbWind);
                obj.ReleaseCOM(wbRain);
                obj.ReleaseCOM(wbLow);
                obj.ReleaseCOM(wbHigh);
                obj.ReleaseCOM(wbWeather);

                obj.ReleaseCOM(Workbooks);

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                if (xlApp != null)
                {
                    xlApp.Quit();
                    Marshal.ReleaseComObject(xlApp);
                    xlApp = null;
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            System.Diagnostics.Debug.WriteLine("天気情報.csv完了");
        }

        public void ReleaseCOM(object o)
        {
            if(o != null)
            {
                Marshal.ReleaseComObject(o);
                o = null;
            }
        }
    }
}
