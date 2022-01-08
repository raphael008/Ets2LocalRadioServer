using System.Text;
using System.Text.RegularExpressions;

namespace LocalRadioServer
{
    public class MusicThread
    {
        public static byte[] segments;
        private static Queue<string> songs = new();

        public static async Task SliceMusicSegment()
        {
            Console.WriteLine("Music thread is running...");
            var songList = Directory.EnumerateFiles(@"F:\music - 2", "*.mp3").ToList();

            var random = new Random();
            songList = songList.OrderBy(s => random.Next()).ToList();
            songList.ForEach(songs.Enqueue);

            while (true)
            {
                var song = songs.Dequeue();
                Console.WriteLine($"Playing {Path.GetFileNameWithoutExtension(song)}");
                
                FileStream fileStream = new(song, FileMode.Open, FileAccess.Read);

                if (ContainsID3Tag(fileStream))
                {
                    int tagSize = GetID3TagSize(fileStream);
                    Console.WriteLine($"{song} has ID3, size: {tagSize}");

                    // read and remove ID3 tag bytes from the stream
                    byte[] tagBytes = new byte[tagSize];
                    fileStream.Read(tagBytes, 0, tagBytes.Length);
                }
                else
                {
                    Console.WriteLine($"{song} no ID3");
                }

                int bitRate = GetBitRate(fileStream);
                Console.WriteLine($"bit rate: {bitRate}");
                fileStream.Close();

                fileStream = new(song, FileMode.Open, FileAccess.Read);
                long bytesToRead = fileStream.Length;
                while (bytesToRead > 0)
                {
                    // 8K per second
                    segments = new byte[8 * 1024];
                    int bytesRead = fileStream.Read(segments, 0, segments.Length);
                    bytesToRead -= bytesRead;

                    Program.Signal.Set();
                    // bitRate Kbps /8 =KBps
                    
                    Thread.Sleep((int) Math.Floor((double) (1000 / (bitRate / 8 / 8))));

                    // Console.WriteLine($"bytesToRead:{bytesToRead} bytesRead:{bytesRead}");
                }
                
                songs.Enqueue(song);
                fileStream.Close();
            }
        }

        private static bool ContainsID3Tag(FileStream fileStream)
        {
            byte[] buffer = new byte[3];
            fileStream.Read(buffer, 0, buffer.Length);
            if (Encoding.UTF8.GetString(buffer) == "ID3")
            {
                return true;
            }

            return false;
        }

        private static int GetID3TagSize(FileStream fileStream)
        {
            byte[] buffer = new byte[7];
            fileStream.Read(buffer, 0, buffer.Length);
            return BitConverter.ToInt32(buffer.Skip(3).Take(4).Reverse().ToArray());
        }

        private static int GetBitRate(FileStream fileStream)
        {
            Dictionary<string, int> bitRateIndexTable = new Dictionary<string, int>();
            bitRateIndexTable.Add("0001", 32);
            bitRateIndexTable.Add("0010", 40);
            bitRateIndexTable.Add("0011", 48);
            bitRateIndexTable.Add("0100", 56);
            bitRateIndexTable.Add("0101", 64);
            bitRateIndexTable.Add("0110", 80);
            bitRateIndexTable.Add("0111", 96);
            bitRateIndexTable.Add("1000", 112);
            bitRateIndexTable.Add("1001", 128);
            bitRateIndexTable.Add("1010", 160);
            bitRateIndexTable.Add("1011", 192);
            bitRateIndexTable.Add("1100", 224);
            bitRateIndexTable.Add("1101", 256);
            bitRateIndexTable.Add("1110", 320);
            
            byte[] buffer = new byte[1024];
            fileStream.Read(buffer, 0, buffer.Length);

            StringBuilder builder = new StringBuilder();
            foreach (var b in buffer)
            {
                builder.Append(Convert.ToString(b, 2));
            }

            Regex pattern = new Regex("1{11}1101[0-9]{17}");
            Match result = pattern.Match(builder.ToString());
            string mp3Header = result.Value;
            Console.WriteLine($"mp3 header: {mp3Header}");
            
            string bitRateIndex = mp3Header.Substring(16, 4);
            Console.WriteLine($"bit rate index: {bitRateIndex}");

            int bitRate = 0;
            bitRateIndexTable.TryGetValue(bitRateIndex, out bitRate);

            return bitRate;
        }
    }
}