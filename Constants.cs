// Documentation:
// Everything: https://ffmpeg.org/ffmpeg-all.html
// VP9: https://trac.ffmpeg.org/wiki/Encode/VP9, https://developers.google.com/media/vp9/settings/vod, https://wiki.webmproject.org/ffmpeg/vp9-encoding-guide
// SVT-AV1: https://gitlab.com/AOMediaCodec/SVT-AV1/-/blob/master/Docs/Parameters.md, https://gitlab.com/AOMediaCodec/SVT-AV1/-/blob/master/Docs/CommonQuestions.md
// AOM-AV1: https://trac.ffmpeg.org/wiki/Encode/AV1
// x265: https://x265.readthedocs.io/en/master/
// x264: http://www.chaneru.com/Roku/HLS/X264_Settings.htm
// VVC: https://github.com/fraunhoferhhi/vvenc/wiki/Usage

static class Constants {
	//Video codecs
	public const string vpxvp9 = "libvpx-vp9";
	public const string aomav1 = "libaom-av1";
	public const string svtav1 = "libsvtav1";
	public const string x265 = "libx265";
	public const string x264 = "libx264";
	public const string vvenc = "libvvenc";

	//Audio codecs
	public const string opus = "libopus";
	public const string flac = "flac";
	public const string mp3 = "libmp3lame";

	//Comparison algorithms
	public const string vmaf = "libvmaf";
	public const string ssim = "ssim";
	public const string xpsnr = "xpsnr";
	public const string ssimulacra2 = "ssimulacra2";
}