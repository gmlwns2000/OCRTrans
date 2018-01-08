using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenCvSharp;
using Util;
using Tesseract;

using Rect = Util.Rect;

namespace OCRTrans
{
    public class FrameArgs
    {
        public bool Break { get; set; } = false;
        public bool DisposeFrame { get; set; } = true;
        public Mat Frame { get; set; }
    }

    public class ScreenCapture : CaptureEngine
    {
        public override event EventHandler<FrameArgs> FrameCaptured;

        public Rect Viewport { get; set; } = new Rect() { X = Screen.PrimaryScreen.Bounds.Left, Y = Screen.PrimaryScreen.Bounds.Top, Width = Screen.PrimaryScreen.Bounds.Width, Height = Screen.PrimaryScreen.Bounds.Height };

        Thread thread;

        public override void Start()
        {
            Stop();

            thread = new Thread(()=>
            {
                while (true)
                {
                    Proc();
                    Thread.Sleep(1);
                }
            });
            thread.IsBackground = true;
            thread.Name = "ScreenCaptureThread";
            thread.Start();
        }

        Bitmap buffer;
        Graphics graphic;
        void Proc()
        {
            var viewport = Viewport;
            if (buffer == null || buffer.Width != viewport.Width || buffer.Height != viewport.Height)
            {
                graphic?.Dispose();
                buffer?.Dispose();

                buffer = new Bitmap((int)viewport.Width, (int)viewport.Height, PixelFormat.Format32bppArgb);
                graphic = Graphics.FromImage(buffer);
            }

            graphic.CopyFromScreen((int)viewport.X, (int)viewport.Y, 0, 0, new System.Drawing.Size((int)viewport.Width, (int)viewport.Height), CopyPixelOperation.SourceCopy);

            var mat = OpenCvSharp.Extensions.BitmapConverter.ToMat(buffer);

            var arg = new FrameArgs() { Frame = mat };
            FrameCaptured?.Invoke(this, arg);
            if (arg.DisposeFrame)
                mat.Dispose();
            if (arg.Break)
            {
                Task.Factory.StartNew(() =>
                {
                    Stop();
                }).Wait();
            }
        }

        public override void Stop()
        {
            if(thread != null)
            {
                thread.Abort();
                thread = null;
            }
        }

        public override void Dispose()
        {
            Stop();
        }
    }

    public abstract class CaptureEngine : IDisposable
    {
        public abstract event EventHandler<FrameArgs> FrameCaptured;

        public abstract void Start();
        public abstract void Stop();

        public abstract void Dispose();
    }

    public class TesseractOcr : OcrEngine
    {
        Language language = Language.English;
        public override Language Language
        {
            get => language;
            set
            {
                if (language != value)
                {
                    language = value;
                    InitEngine();
                }
            }
        }

        TesseractEngine Engine;
        public TesseractOcr()
        {
            InitEngine();
        }

        void InitEngine()
        {
            Engine?.Dispose();

            Engine = new TesseractEngine(@".\tessdata", GetLanguageString(Language), EngineMode.Default);
        }

        string GetLanguageString(Language lang)
        {
            switch (lang)
            {
                case Language.English:
                    return "eng";
                case Language.Korean:
                    return "kor";
                case Language.Japaness:
                    return "jpn";
                default:
                    throw new NotImplementedException();
            }
        }

        Bitmap buffer;
        public override OcrResult Run(Mat frame)
        {
            //preproc
            Cv2.CvtColor(frame, frame, ColorConversionCodes.BGRA2GRAY);

            if(buffer == null || buffer.Width != frame.Width || buffer.Height != frame.Height)
            {
                buffer = new Bitmap(frame.Width, frame.Height, PixelFormat.Format8bppIndexed);
            }
            OpenCvSharp.Extensions.BitmapConverter.ToBitmap(frame, buffer);
            Profiler.Count("OcrFps");
            Profiler.Start("OcrProcess");
            using (var page = Engine.Process(buffer, PageSegMode.Auto))
            {
                Profiler.End("OcrProcess");

                Logger.Log("inferenced");

                using (var iter = page.GetIterator())
                {
                    var confidence = page.GetMeanConfidence();
                    var result = new OcrResult() { MeanConfidence = confidence };
                    
                    iter.Begin();
                    do
                    {
                        var block = new OcrBlock();
                        do
                        {
                            var para = new OcrPara();
                            do
                            {
                                var line = new OcrLine();
                                do
                                {
                                    var textWord = iter.GetText(PageIteratorLevel.Word);
                                    var textBoundResult = iter.TryGetBoundingBox(PageIteratorLevel.Word, out Tesseract.Rect textBound);
                                    var text = new OcrWord() { Text = textWord };
                                    if (textBoundResult)
                                        text.SetRect(textBound);
                                    line.Word.Add(text);
                                    
                                    if (iter.IsAtFinalOf(PageIteratorLevel.TextLine, PageIteratorLevel.Word))
                                    {
                                        var lineBoundResult = iter.TryGetBoundingBox(PageIteratorLevel.TextLine, out Tesseract.Rect lineBound);
                                        if (lineBoundResult)
                                            line.SetRect(lineBound);
                                        para.Line.Add(line);
                                        line = new OcrLine();
                                    }
                                } while (iter.Next(PageIteratorLevel.TextLine, PageIteratorLevel.Word));

                                if (iter.IsAtFinalOf(PageIteratorLevel.Para, PageIteratorLevel.TextLine))
                                {
                                    var paraBoundResult = iter.TryGetBoundingBox(PageIteratorLevel.Para, out Tesseract.Rect paraBound);
                                    if (paraBoundResult)
                                        para.SetRect(paraBound);
                                    block.Para.Add(para);
                                    para = new OcrPara();
                                }
                            } while (iter.Next(PageIteratorLevel.Para, PageIteratorLevel.TextLine));

                            if (iter.IsAtFinalOf(PageIteratorLevel.Block, PageIteratorLevel.Para))
                            {
                                var blockBoundResult = iter.TryGetBoundingBox(PageIteratorLevel.Block, out Tesseract.Rect blockBound);
                                if (blockBoundResult)
                                    block.SetRect(blockBound);
                                result.Block.Add(block);
                                block = new OcrBlock();
                            }
                        } while (iter.Next(PageIteratorLevel.Block, PageIteratorLevel.Para));
                    } while (iter.Next(PageIteratorLevel.Block));

                    return result;
                }
            }
        }

        public override void Dispose()
        {
            Engine?.Dispose();
            Engine = null;

            buffer?.Dispose();
            buffer = null;
        }
    }

    public abstract class OcrElement
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        public abstract string GetText();

        public void SetRect(Tesseract.Rect rect)
        {
            X = rect.X1;
            Y = rect.Y1;
            Width = rect.Width;
            Height = rect.Height;
        }

        public override string ToString()
        {
            return GetText();
        }
    }

    public class OcrBlock : OcrElement
    {
        public List<OcrPara> Para { get; set; } = new List<OcrPara>();
        public override string GetText()
        {
            StringBuilder builder = new StringBuilder();
            foreach (var item in Para)
            {
                builder.Append(item.GetText());
                builder.Append('\n');
            }
            string ret = builder.ToString();
            ret.TrimEnd('\n');
            return ret;
        }
    }

    public class OcrPara : OcrElement
    {
        public List<OcrLine> Line { get; set; } = new List<OcrLine>();
        public override string GetText()
        {
            StringBuilder builder = new StringBuilder();
            foreach (var item in Line)
            {
                builder.Append(item.GetText());
                builder.Append('\n');
            }
            string ret = builder.ToString();
            ret = ret.TrimEnd('\n');
            return ret;
        }
    }

    public class OcrLine : OcrElement
    {
        public List<OcrWord> Word { get; set; } = new List<OcrWord>();
        public override string GetText()
        {
            StringBuilder builder = new StringBuilder();
            foreach (var item in Word)
            {
                builder.Append(item.GetText());
                builder.Append(' ');
            }
            var ret = builder.ToString();
            ret = ret.TrimEnd();
            return ret;
        }
    }

    public class OcrWord : OcrElement
    {
        public string Text { get; set; }
        public override string GetText()
        {
            return Text;
        }
    }

    public class OcrResult
    {
        public double MeanConfidence { get; set; }
        public List<OcrBlock> Block { get; set; } = new List<OcrBlock>();

        public string GetText()
        {
            StringBuilder builder = new StringBuilder();
            foreach (var item in Block)
            {
                var str = item.GetText();
                builder.AppendLine(str);
            }
            return builder.ToString();
        }
    }

    public abstract class OcrEngine : IDisposable
    {
        public virtual Language Language { get; set; } = Language.English;

        public abstract OcrResult Run(Mat frame);
        public abstract void Dispose();
    }

    public class TranslatedArg : FrameArgs
    {
        public OcrResult Original { get; set; }
        public OcrResult Results { get; set; }
    }

    public class OcrTranslater : IDisposable
    {
        public Language From
        {
            get => ocr.Language;
            set => ocr.Language = value;
        }
        public Language To { get; set; } = Language.Korean;

        public event EventHandler<FrameArgs> FrameReady;
        public event EventHandler<TranslatedArg> Translated;

        public Rect Viewport { get => capture.Viewport; set => capture.Viewport = value; }

        Translater translater;
        ScreenCapture capture;
        TesseractOcr ocr;

        public OcrTranslater()
        {
            ocr = new TesseractOcr();
            capture = new ScreenCapture();
            translater = new Translater();

            capture.FrameCaptured += Capture_FrameCaptured;
        }

        void Capture_FrameCaptured(object sender, FrameArgs e)
        {
            FrameReady?.Invoke(sender, e);

            var detected = ocr.Run(e.Frame);
            var translated = new OcrResult();
            foreach (var block in detected.Block)
            {
                var transBlock = new OcrBlock();
                foreach (var para in block.Para)
                {
                    var transPara = new OcrPara();
                    foreach (var line in para.Line)
                    {
                        var text = line.GetText();
                        var transText = translater.TranslateGoogle(text, From, To);
                        var transLine = new OcrLine();
                        var transWord = new OcrWord() { Text = transText };
                        transLine.Word.Add(transWord);
                        transPara.Line.Add(transLine);
                    }
                    transBlock.Para.Add(transPara);
                }
                translated.Block.Add(transBlock);
            }

            var arg = new TranslatedArg()
            {
                Frame = e.Frame,
                Original = detected,
                Results = translated
            };

            var str = translated.GetText();
            Logger.Log(str ?? "null");

            Translated?.Invoke(this, arg);
            e.DisposeFrame = arg.DisposeFrame;
            e.Break = arg.Break;
        }

        public void Start()
        {
            capture.Start();
        }

        public void Stop()
        {
            capture.Stop();
        }

        public void Dispose()
        {
            capture?.Dispose();
            capture = null;

            ocr?.Dispose();
            ocr = null;

            translater?.Dispose();
            translater = null;
        }
    }
}
