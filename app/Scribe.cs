using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Text;

namespace CapriDesktop
{
    [DataContract]
    internal sealed class ScribeReply
    {
        [DataMember(Name = "text")] public string Text;
    }

    // ElevenLabs speech-to-text (Scribe): better ears than the offline ones when an ElevenLabs key is set.
    internal static class Scribe
    {
        internal const string Model = "scribe_v1";
        internal const string Route = "/v1/speech-to-text";

        // Sends the multipart body with their key; answers with the HTTP status and the reply text (null when nothing came back).
        internal static Func<string, string, byte[], Tuple<int, string>> Post = Send;

        private static Tuple<int, string> Send(string key, string contentType, byte[] body)
        {
            HttpWebRequest request = Voice.Open(Route, key, "POST");
            request.Timeout = 30000;
            request.ReadWriteTimeout = 30000;
            request.ContentType = contentType;
            request.ContentLength = body.Length;
            try
            {
                using (Stream upload = request.GetRequestStream()) upload.Write(body, 0, body.Length);
                using (WebResponse response = request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8)) return Tuple.Create(200, reader.ReadToEnd());
            }
            catch (WebException error)
            {
                HttpWebResponse failed = error.Response as HttpWebResponse;
                return Tuple.Create(failed != null ? (int)failed.StatusCode : 0, (string)null);
            }
            catch (IOException) { return Tuple.Create(0, (string)null); }
        }

        internal static string Boundary() { return "capri" + Guid.NewGuid().ToString("N"); }

        internal static byte[] Body(string boundary, byte[] wav)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                Field(stream, boundary, "model_id", Model);
                Field(stream, boundary, "tag_audio_events", "false");
                Write(stream, "--" + boundary + "\r\nContent-Disposition: form-data; name=\"file\"; filename=\"speech.wav\"\r\nContent-Type: audio/wav\r\n\r\n");
                stream.Write(wav, 0, wav.Length);
                Write(stream, "\r\n--" + boundary + "--\r\n");
                return stream.ToArray();
            }
        }

        private static void Field(Stream stream, string boundary, string name, string value)
        {
            Write(stream, "--" + boundary + "\r\nContent-Disposition: form-data; name=\"" + name + "\"\r\n\r\n" + value + "\r\n");
        }

        private static void Write(Stream stream, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        }

        internal static string Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) throw new IOException("ElevenLabs sent no words back.");
            ScribeReply reply;
            try { reply = Voice.Read<ScribeReply>(Encoding.UTF8.GetBytes(json)); }
            catch (SerializationException) { throw new IOException("ElevenLabs speech-to-text sent a reply Capri couldn't read."); }
            return reply == null ? "" : reply.Text ?? "";
        }

        // The words in one utterance (16 kHz mono samples). Throws when ElevenLabs can't be reached or says no, so the caller can fall back.
        internal static string Hear(string key, short[] samples)
        {
            string boundary = Boundary();
            byte[] body = Body(boundary, Voice.Wave(samples, Ears.Rate));
            Tuple<int, string> answer = Post(key, "multipart/form-data; boundary=" + boundary, body);
            if (answer.Item1 != 200) throw new IOException("ElevenLabs speech-to-text said " + (answer.Item1 == 0 ? "nothing" : "HTTP " + answer.Item1) + ".");
            return Parse(answer.Item2);
        }
    }
}
