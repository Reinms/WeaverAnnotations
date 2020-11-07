namespace WeaverAnnotations.Util.Logging
{
    using System;

    using WeaverAnnotations.Util.Xtn;

    public interface ILogProvider
    {
        void Log(LogType logType, String text);
    }

    public enum LogType { Debug, Info, Message, Warning, Error, Fatal }

    public static class LogTypeXtn
    {


        public static (Byte r, Byte g, Byte b) Col(this LogType type) => type switch
        {
            LogType.Debug => (120, 120, 120),
            LogType.Info => (0, 0, 255),
            LogType.Message => (0, 255, 0),
            LogType.Warning => (120, 120, 0),
            LogType.Error => (255, 0, 0),
            LogType.Fatal => (120, 0, 0),
            _ => (0, 0, 0),
        };

        public static ConsoleColor ConsoleCol(this (Byte r, Byte g, Byte b) color)
        {
            (Single r, Single g, Single b) fracs = color.ToFrac();
            Single mag = fracs.Magnitude();
            Int32 i = mag > 0.85f ? 1 : 0;
            (Int32 r, Int32 g, Int32 b) = fracs.ToBits();
            Int32 ccol = 0
                |(b)
                |(g << 1)
                |(r << 2)
                |(i << 3);

            return (ConsoleColor)ccol switch
            {
                ConsoleColor.Gray => ConsoleColor.DarkGray,
                ConsoleColor.DarkGray => ConsoleColor.Gray,
                ConsoleColor c => c,
            };
        }

        public static ConsoleColor ConsoleCol(this LogType type) => type.Col().ConsoleCol();



        private static (Single r, Single g, Single b) ToFrac(this (Byte r, Byte g, Byte b) color) => color.Map(ToFrac);
        private static (Int32, Int32, Int32) ToBits(this (Single r, Single g, Single b) color) => color.Normalize().Map(ToBit);
        private static Int32 ToBit(this Single s) => s >= 0.55f ? 1 : 0;


        private static Single ToFrac(this Byte value) => value / (Single)Byte.MaxValue;
        private static Single Magnitude(this (Single x, Single y, Single z) vec) => MathF.Sqrt(vec.x * vec.x + vec.y * vec.y + vec.z * vec.z);
        private static (Single x, Single y, Single z) Normalize(this (Single x, Single y, Single z) vec)
        {
            Single mag = vec.Magnitude();
            if(mag == 0f) return (0f, 0f, 0f);
            return vec.Multiply(1f / mag);
        }
        private static (Single x, Single y, Single z) Multiply(this (Single x, Single y, Single z) vec, Single value) => (vec.x * value, vec.y * value, vec.z * value);
    }

    public static class LogXtn
    {
        private static void _Log<T>(this T? logger, LogType type, String text)
            where T : ILogProvider
            => logger?.Log(type, text);

        public static void Debug<T>(this T? logger, String text)
            where T : ILogProvider
            => logger._Log(LogType.Debug, text);

        public static void Info<T>(this T? logger, String text)
            where T : ILogProvider
            => logger._Log(LogType.Info, text);

        public static void Message<T>(this T? logger, String text)
            where T : ILogProvider
            => logger._Log(LogType.Message, text);

        public static void Warning<T>(this T? logger, String text)
            where T : ILogProvider
            => logger._Log(LogType.Warning, text);

        public static void Error<T>(this T? logger, String text)
            where T : ILogProvider
            => logger._Log(LogType.Error, text);

        public static void Fatal<T>(this T? logger, String text)
            where T : ILogProvider
            => logger._Log(LogType.Fatal, text);
    }
}
