using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FormatCovidCsv
{
    class Program
    {
        static void Main(string[] args)
        {
            string strCsvPath = $"{AppDomain.CurrentDomain.BaseDirectory}";
            strCsvPath = strCsvPath.Substring(0, strCsvPath.IndexOf(@"OpendataDownLoader")) + @"シーン\Data\";
            string strTmpCsv = strCsvPath + "新規陽性者数tmp.csv";
            string strCsv = strCsvPath + "新規陽性者数.csv";

            try
            {
                using (System.IO.StreamReader streamReader = new System.IO.StreamReader(strTmpCsv))
                {
                    // 列の説明行は＃を付けてコメントアウト
                    string header = streamReader.ReadLine();
                    using(var sw = new System.IO.StreamWriter(strCsv, false, System.Text.Encoding.GetEncoding("shift-jis")))
                    {
                        sw.Write($"#{header},年,月,日{Environment.NewLine}");
                    }

                    while (!streamReader.EndOfStream)
                    {
                        Console.WriteLine("新規陽性者数データ処理中");
                        string line = streamReader.ReadLine();
                        string[] date = line.Substring(0, line.IndexOf(",")).Split('/');
                        line = line.Substring(line.IndexOf(","));

                        if (date[1].Length != 2)
                        {
                            date[1] = "0" + date[1];
                        }
                        if (date[2].Length != 2)
                        {
                            date[2] = "0" + date[2];
                        }
                        line = date[0] + "/" + date[1] + "/" + date[2] + line + ","+ date[0] + "," + date[1] + "," + date[2];

                        using(var sw = new System.IO.StreamWriter(strCsv, true, System.Text.Encoding.GetEncoding("shift-jis")))
                        {
                            sw.Write(line + Environment.NewLine);
                        }
                    }
                }
                System.IO.File.Delete(strTmpCsv);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
