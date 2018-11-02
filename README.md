# VideoJoiner

基於 .NET Framework 4.6 與 [NReco.VideoConverter](https://www.nrecosite.com/video_converter_net.aspx)，將指定時間區間內影片合併的小工具。

# 環境需求

* [Microsoft .NET Framework 4.6](https://www.microsoft.com/zh-tw/download/details.aspx?id=48137) or latest version

# 檔案結構

```
─┬─ Joins\                      (合併結果將置入此資料夾)
 ├─ ffmpeg.exe                  (程式執行後產生)
 ├─ NReco.VideoConverter.dll
 └─ VideoJoiner.exe
```

# 參數說明

* `--path`, `-p`：原始影片資料夾路徑\
  預設值：執行路徑
* `--inputs_type`, `-it`：輸入影片檔案類型\
  預設值：`mov`
* `--output_type`, `-ot`：輸出影片檔案類型\
  預設值：`mp4`
* `--interval`, `-i`：影片時間容許區間（分）\
  預設值：`3`
* `--preset`：影片編碼速度，越慢則壓縮效率越高\
  預設值：`median`\
  其他值：`UltraFast`, `SuperFast`, `VeryFast`, `Faster`, `Fast`, `Medium`, `Slow`, `Slower`, `VerySlow`
* `--crf`：恆定質量值（Constant Rate Factor）\
  預設值：`18`\
  其他值：`0` 至 `51` 間之整數值
* `--aq`：音訊品質，越高的設定值會得到越高的輸出品質與位元率大小\
  預設值：`10`\
  其他值：`0.1` 至 `10` 間數值，小數點取至小數第一位（多餘部分將四捨五入）

# 程式說明

> **請注意檔案格式須為 `YYYYMMdd_HHmmss.*`。**

程式執行後，將會先解析目標資料夾內影片並進行分類，舉例而言，若當目標資料夾影片檔案如下：

```
─┬─ 20181030_051753.mov
 ├─ 20181030_052054.mov
 ├─ 20181030_052354.mov
 ├─ 20181030_220524.mov
 ├─ 20181030_220824.mov
 └─ 20181030_224031.mov
```

則解析結果將為：

```
─┬─ 20181030_051753
 │      ├─ 20181030_051753.mov
 │      ├─ 20181030_052054.mov
 │      └─ 20181030_052354.mov
 ├─ 20181030_220524
 │      ├─ 20181030_220524.mov
 │      └─ 20181030_220824.mov
 └─ 20181030_224031
        └─ 20181030_224031.mov
```

因此當程式執行完畢後，將會產生 `20181030_051753.*`、`20181030_220524.*` 與 `20181030_224031.*` 三部影片檔案於 `.\Joins\` 資料夾內，其中 `*` 為參數 `output_type` 值。

![videojoiner-demo](https://blog.holey.cc/2018/11/02/videojoiner/videojoiner-demo.png)