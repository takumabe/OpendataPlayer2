# PRISM4 Default Schema --- Lambda Systems Inc.
Title "Default"
#CommonScenePath ""
ScenePath ""
ImagePath "..\\Image"
DataPath  "..\\Data"
TexturePath	"D:\\Lambda\\Texture"
LogoPath "D:\\Lambda\\Bin.V2\\Logo"
ThumbNailPath ""
OADevice	"GRID-EX32HD"
SceneIO		"Lambda.SioLocal.SceneIO"
# UseSceneFontCache
UseDevice Lambda.PcSound.Player { At Break Stop() }

Source "都道府県" {
  Csv { , Comment "#" }
  SysVar {}
  Struct {
	Column "id" { L Ascent }
	Column "name" { S 256 }
	Column "X" { L }
	Column "Y" { L }
	Column "file name" { S 32 }
  }
}
Source "corona" {
  Csv { , Comment "#" }
  SysVar {}
  Struct {
	Column "日付" { S 32 Descent }
	Column "全国" { L }
	Column "北海道" { L }
	Column "青森" { L }
	Column "岩手" { L }
	Column "宮城" { L }
	Column "秋田" { L }
	Column "山形" { L }
	Column "福島" { L }
	Column "茨城" { L }
	Column "栃木" { L }
	Column "群馬" { L }
	Column "埼玉" { L }
	Column "千葉" { L }
	Column "東京" { L }
	Column "神奈川" { L }
	Column "新潟" { L }
	Column "富山" { L }
	Column "石川" { L }
	Column "福井" { L }
	Column "山梨" { L }
	Column "長野" { L }
	Column "岐阜" { L }
	Column "静岡" { L }
	Column "愛知" { L }
	Column "三重" { L }
	Column "滋賀" { L }
	Column "京都" { L }
	Column "大阪" { L }
	Column "兵庫" { L }
	Column "奈良" { L }
	Column "和歌山" { L }
	Column "鳥取" { L }
	Column "島根" { L }
	Column "岡山" { L }
	Column "広島" { L }
	Column "山口" { L }
	Column "徳島" { L }
	Column "香川" { L }
	Column "愛媛" { L }
	Column "高知" { L }
	Column "福岡" { L }
	Column "佐賀" { L }
	Column "長崎" { L }
	Column "熊本" { L }
	Column "大分" { L }
	Column "宮崎" { L }
	Column "鹿児島" { L }
	Column "沖縄" { L }
  }
}

Source "天気情報" {
  Csv { , Comment "#" }
  SysVar {}
  Struct {
	Column "観測所番号" { L Ascent }
	Column "地点" { S 32 }
	Column "今日の最高気温" { L }
	Column "今日の最低気温" { L }
	Column "現在の降水量" { L }
	Column "今日の最大風速" { L }
  }
}


DataTable "コロナの情報" { "corona" "新規陽性者数.csv" }
DataTable "都道府県情報" { "都道府県" "都道府県.csv" }
DataTable "天気情報" { "天気情報" "天気情報.csv" }