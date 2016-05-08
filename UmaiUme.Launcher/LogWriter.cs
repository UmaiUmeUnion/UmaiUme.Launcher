using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace UmaiUme.Launcher
{
    public class LogWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        private readonly StreamWriter console, file;

        public LogWriter(StreamWriter consoleOutput, string fileName)
        {
            console = consoleOutput;
            file = File.CreateText(fileName);
            file.AutoFlush = true;
        }

        protected override void Dispose(bool disposing)
        {
            console.Dispose();
            file.Dispose();
            base.Dispose(disposing);
        }

        public override void Close()
        {
            console.Close();
            file.Close();
            base.Close();
        }

        public override void Write(char value)
        {
            console.Write(value);
            file.Write(value);
            base.Write(value);
        }
    }
}
