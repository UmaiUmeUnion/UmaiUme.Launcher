namespace UmaiUme.Launcher.Utils
{
    public static class StringUtils
    {
        public static bool IsNullOrWhiteSpace(this string str)
        {
            return str == null || str.Trim() == string.Empty;
        }
    }
}