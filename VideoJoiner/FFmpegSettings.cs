namespace VideoJoiner
{
    class FFMpegSettings
    {
        /// <summary>
        /// Video converter.
        /// </summary>
        public string VideoConverter { get; private set; } = "libx264";

        /// <summary>
        /// Preset 指定的編碼速度越慢，獲得的壓縮效率就越高。
        /// 
        /// ref. https://blog.csdn.net/daixinmei/article/details/51886850
        /// </summary>
        public PresetType Preset { get; set; }

        /// <summary>
        /// 在優先保證畫質（並不在乎編碼時間）的情況下，使用 -crf 參數來控制編碼較為適合。
        /// 其中 0 為無損模式，數值越大畫質越差、產生的檔案越小。主觀上，18-28 是一個合理
        /// 的範圍。18 被認為是視覺無損（技術上依然有損），他的輸出影像質量和輸入影像相當。
        /// 
        /// ref. https://blog.csdn.net/happydeer/article/details/52610060
        /// </summary>
        public int CRFScale { get; set; }

        /// <summary>
        /// Native FFmpeg AAC encoder
        /// 原生 (Native) FFmpeg AAC 編碼器，這是目前 ffmpeg 所能提供的第二高品質 AAC
        /// 編碼器。而且它已包含在 ffmpeg 內，不像本文中其它 AAC 編碼器那樣需要一個外部程
        /// 式庫。在 128kbps 位元速率通常可以產生與 libfdk_aac 相同甚至更高的品質，但在
        /// 96kbps 以下偶爾聽起來會比較差。這是預設的 AAC 編碼器。缺點是此編碼器還不支援
        /// AAC-HE profile.
        /// 
        /// ref. https://www.mobile01.com/topicdetail.php?f=510&t=4509267
        /// </summary>
        public string AudioConverter { get; private set; } = "aac";

        /// <summary>
        /// 音訊品質。越高的設定值會得到越高的輸出品質與位元率大小。
        /// </summary>
        public decimal AudioQuality { get; set; }

        /// <summary>
        /// Audio bitrate.
        /// </summary>
        public int AudioBitrate { get; private set; }
        
        public FFMpegSettings()
        {
            this.Preset = PresetType.Medium;
            this.CRFScale = 18;
            this.AudioQuality = 10;
        }

        public FFMpegSettings(PresetType preset, int crf, int audioQuality)
        {
            this.Preset = preset;
            this.CRFScale = crf;
            this.AudioQuality = audioQuality;
        }

        public string GetArgsLine() =>
            $" -c:v { VideoConverter } -preset { Preset.ToString().ToLower() } -crf { CRFScale } -c:a { AudioConverter } -q:a { AudioQuality.ToString("F0") } ";

        public enum PresetType
        {
            UltraFast,
            SuperFast,
            VeryFast,
            Faster,
            Fast,
            Medium,
            Slow,
            Slower,
            VerySlow
        }
    }
}
