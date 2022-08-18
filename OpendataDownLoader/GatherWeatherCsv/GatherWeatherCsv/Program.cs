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
        public enum eColumn
        {
            ID,
            Prefecture,
            Spot,
            GlobalID,
            Year,
            Month,
            Date,
            Hour,
            Minute,
            Data
        };

        static void Main(string[] args)
        {
            Console.WriteLine("天気情報.csv生成タスク開始");


            string strDataDir = $"{AppDomain.CurrentDomain.BaseDirectory}";
            strDataDir = strDataDir.Substring(0, strDataDir.LastIndexOf("OpendataDownLoader")) + @"シーン\Data\";
            string[] aryCsvNames = new string[]
            {
                strDataDir + "最高気温.csv", strDataDir + "最低気温.csv", strDataDir + "日降水量.csv", strDataDir + "最大風速.csv"
            };

            int[] aryRegionID = new int[]
            {
                // 各都道府県の県庁所在地の観測所番号リスト.
                14163, 19432, 31312, 32402, 33431, 34392, 35426, 36127, 40201, 41277,
                42251, 43241, 44132, 45212, 46106, 48156, 49142, 50331, 51106, 52586,
                53133, 54232, 55102, 56227, 57066, 60216, 61286, 62078, 63518, 64036,
                65042, 66408, 67437, 68132, 69122, 71106, 72086, 73166, 74181, 81286,
                82182, 83216, 84496, 85142, 86141, 87376, 88317, 91197
            };

            // 各csvから取り出したデータを保存する配列.
            string[] aryHighData = new string[aryRegionID.Length];
            string[] aryLowData = new string[aryRegionID.Length];
            string[] aryRainData = new string[aryRegionID.Length];
            string[] aryWindData = new string[aryRegionID.Length];

            // 列の説明コメント追加
            using (System.IO.StreamWriter sw = new System.IO.StreamWriter(strDataDir + "天気情報.csv", false, System.Text.Encoding.GetEncoding("shift-jis")))
            {
                sw.Write("#観測所番号,地点,今日の最高気温(℃),今日の最低気温(℃),現在の降水量(mm),今日の最大風速(m/s),年,月,日,時,分" + Environment.NewLine);
            }

            // 各csvから必要な情報を取り出す.
            for (int nCsvIndex = 0; nCsvIndex < aryCsvNames.Length; nCsvIndex++)
            {
                int nRegionIndex = 0;

                using (var sReader = new System.IO.StreamReader(aryCsvNames[nCsvIndex], System.Text.Encoding.GetEncoding("shift-jis")))
                {
                    // 最初の行は列の説明なので飛ばす.
                    sReader.ReadLine();

                    while (!sReader.EndOfStream)
                    {
                        string strBuffer = sReader.ReadLine();
                        string[] aryCsvBuffer = strBuffer.Split(',');
                        if (aryCsvBuffer[(int)eColumn.ID] == aryRegionID[nRegionIndex].ToString())
                        {
                            // 県庁所在地のリストと一致したら保存

                            if (nCsvIndex == 0)
                            {
                                aryHighData[nRegionIndex] = aryCsvBuffer[(int)eColumn.ID] + "," + aryCsvBuffer[(int)eColumn.Spot].Substring(0, aryCsvBuffer[(int)eColumn.Spot].IndexOf("（")) + "," + aryCsvBuffer[(int)eColumn.Data];
                            }
                            else if (nCsvIndex == 1)
                            {
                                aryLowData[nRegionIndex] = aryCsvBuffer[(int)eColumn.Data];
                            }
                            else if (nCsvIndex == 2)
                            {
                                aryRainData[nRegionIndex] = aryCsvBuffer[(int)eColumn.Data];
                            }
                            else if (nCsvIndex == aryCsvNames.Length - 1)
                            {
                                aryWindData[nRegionIndex] = aryCsvBuffer[(int)eColumn.Data] + "," + aryCsvBuffer[(int)eColumn.Year] + "," + aryCsvBuffer[(int)eColumn.Month] + "," + aryCsvBuffer[(int)eColumn.Date] + "," + aryCsvBuffer[(int)eColumn.Hour] + "," + aryCsvBuffer[(int)eColumn.Minute];
                            }

                            if (aryRegionID.Length - 1 <= nRegionIndex)
                            {
                                break;
                            }
                            else
                            {
                                nRegionIndex++;
                            }
                        }
                    }
                }
            }

            // 保存したデータを書き込み.
            using (System.IO.StreamWriter sWriter = new System.IO.StreamWriter(strDataDir + "天気情報.csv", true, System.Text.Encoding.GetEncoding("shift-jis")))
            {
                for (int nIndex = 0; nIndex < aryRegionID.Length; nIndex++)
                {
                    string strData = aryHighData[nIndex] + "," + aryLowData[nIndex] + "," + aryRainData[nIndex] + "," + aryWindData[nIndex] + Environment.NewLine;
                    sWriter.Write(strData);
                }
            }

            Console.WriteLine("天気情報.csv生成タスク終了");
        }
    }
}
