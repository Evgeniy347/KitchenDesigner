#nullable disable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using SimpleGif.Data;
using SimpleGif.Enums;
using SimpleGif.GifCore;
using SimpleGif.GifCore.Assets.GifCore;
using SimpleGif.GifCore.Blocks;

namespace SimpleGif.Data
{
	/// <summary>
	/// Stub for Color32 from UnityEngine.CoreModule
	/// </summary>
	public struct Color32 : IEquatable<Color32>
	{
		// ReSharper disable InconsistentNaming (original naming saved)
		public readonly byte r;
		public readonly byte g;
		public readonly byte b;
		public readonly byte a;
		// ReSharper restore InconsistentNaming

		public Color32(byte r, byte g, byte b, byte a)
		{
			this.r = r;
			this.g = g;
			this.b = b;
			this.a = a;
		}

		public bool Equals(Color32 other)
		{
			return a == 0 && other.a == 0 || r == other.r && g == other.g && b == other.b && a > 0 && other.a > 0;
		}

		public override bool Equals(object obj)
		{
			if (obj == null || GetType() != obj.GetType()) return false;

			var other = (Color32) obj;

			return Equals(other);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return a == 0 ? 0 : r + 256 * g + 65536 * b;
			}
		}
	}
}
namespace SimpleGif.Data
{
	/// <summary>
	/// Stub for Texture2D from UnityEngine.CoreModule
	/// </summary>
	public class Texture2D
	{
		// ReSharper disable once InconsistentNaming (original naming saved)
		public readonly int width;

		// ReSharper disable once InconsistentNaming (original naming saved)
		public readonly int height;
		
		private Color32[] _pixels;

		public Texture2D(int width, int height)
		{
			this.width = width;
			this.height = height;
		}

		public void SetPixels32(Color32[] pixels)
		{
			_pixels = pixels.ToArray();
		}

		public Color32[] GetPixels32()
		{
			return _pixels.ToArray();
		}

		public void Apply()
		{
		}
	}
}
namespace SimpleGif.Data
{
	/// <summary>
	/// Stub for Texture2D from UnityEngine.CoreModule
	/// </summary>
	public class IndexedTexture2D
    {
		// ReSharper disable once InconsistentNaming (original naming saved)
		public readonly int width;

		// ReSharper disable once InconsistentNaming (original naming saved)
		public readonly int height;

        private Color32[] _colorTable;
        private int[] _colorIndexes;

		public IndexedTexture2D(int width, int height)
		{
			this.width = width;
			this.height = height;
		}

		public void SetPixels32(Color32[] colorTable, int[] colorIndexes)
		{
		    _colorTable = colorTable.ToArray();
		    _colorIndexes = colorIndexes.ToArray();
		}

		public Color32[] GetPixels32()
		{
            var emptyColor = new Color32();

			return _colorIndexes.Select(i => i == -1 ? emptyColor : _colorTable[i]).ToArray();
		}

		public void Apply()
		{
		}
	}
}
namespace SimpleGif.Data
{
	/// <summary>
	/// Texture + delay + disposal method
	/// </summary>
	public class GifFrame
	{
		public Texture2D Texture;
		public float Delay;
		public DisposalMethod DisposalMethod = DisposalMethod.RestoreToBackgroundColor;

		public void ApplyPalette(MasterPalette palette)
		{
			TextureConverter.ConvertTo8Bits(ref Texture, palette);
		}
	}
}
namespace SimpleGif.Data
{
	public class EncodeProgress
	{
		public int Progress;
		public int FrameCount;
		public bool Completed;
		public byte[] Bytes;
		public Exception Exception;
	}
}
namespace SimpleGif.Data
{
	public class DecodeProgress
	{
		public int Progress;
		public int FrameCount;
		public bool Completed;
		public Gif Gif;
		public Exception Exception;
	}
}
namespace SimpleGif.Enums
{
	/// <summary>
	/// These are selections of colors based in evenly ordered RGB levels which provide complete RGB combinations,
	/// mainly used as master palettes to display any kind of image within the limitations of the 8-bit pixel depth.
	/// More info: https://en.wikipedia.org/wiki/List_of_software_palettes#RGB_arrangements
	/// </summary>
	public enum MasterPalette
	{
		DontApply,
		Levels666,
		Levels676,
		Levels685,
		Levels884,
		Grayscale
	}
}
namespace SimpleGif.Enums
{
	/// <summary>
	/// Indicates the way in which the graphic is to be treated after being displayed.
	/// More info: https://www.w3.org/Graphics/GIF/spec-gif89a.txt
	/// </summary>
	public enum DisposalMethod
	{
		/// <summary>
		/// The decoder is not required to take any action.
		/// </summary>
		NoDisposalSpecified = 0,

		/// <summary>
		/// The graphic is to be left in place.
		/// </summary>
		DoNotDispose = 1,

		/// <summary>
		/// The area used by the graphic must be restored to the background color.
		/// </summary>
		RestoreToBackgroundColor = 2,

		/// <summary>
		/// The decoder is required to restore the area overwritten by the graphic with what was there prior to rendering the graphic.
		/// </summary>
		RestoreToPrevious = 3
	}
}
namespace SimpleGif
{
	/// <summary>
	/// Simple class for working with GIF format
	/// </summary>
	public class Gif
	{
	    private static bool _free = false;

		/// <summary>
		/// List of GIF frames
		/// </summary>
		public List<GifFrame> Frames;

		/// <summary>
		/// Create a new instance from GIF frames.
		/// </summary>
		public Gif(List<GifFrame> frames)
		{
			Frames = frames;
		}

		/// <summary>
		/// Decode byte array and return a new instance.
		/// </summary>
		public static Gif Decode(byte[] bytes)
		{
			return new Gif(DecodeIterator(bytes).ToList());
		}

		/// <summary>
		/// Decode byte array in multiple threads.
		/// </summary>
		public static void DecodeParallel(byte[] bytes, Action<DecodeProgress> onProgress) // TODO: Refact
		{
		    if (_free)
		    {
		        throw new Exception("The Free version doesn't support this feature. Please consider buying the Full version of Power GIF.");
		    }

            GifParser parser;

			try
            {
                parser = new GifParser(bytes);
			}
            catch (Exception e)
            {
				onProgress(new DecodeProgress { Exception = e, Completed = true });
				return;
            }

            var decoded = new Dictionary<ImageDescriptor, byte[]>();
			var frameCount = parser.Blocks.Count(i => i is ImageDescriptor);
			var decodeProgress = new DecodeProgress { FrameCount = frameCount };

			for (var i = 0; i < parser.Blocks.Count; i++)
			{
				var imageDescriptor = parser.Blocks[i] as ImageDescriptor;

				if (imageDescriptor == null) continue;

				var data = (TableBasedImageData) parser.Blocks[i + 1 + imageDescriptor.LocalColorTableFlag];

                ThreadPool.QueueUserWorkItem(context =>
                {
                    if (decodeProgress.Completed || decodeProgress.Exception != null) return;

                    byte[] colorIndexes;

                    try
                    {
                        colorIndexes = LzwDecoder.Decode(data.ImageData, data.LzwMinimumCodeSize);
                    }
                    catch (Exception e)
                    {
                        decodeProgress.Exception = e;
                        decodeProgress.Completed = true;
						onProgress(decodeProgress);
						return;
                    }

                    lock (decoded)
                    {
                        decoded.Add(imageDescriptor, colorIndexes);
                        decodeProgress.Progress++;

                        if (decoded.Count == frameCount)
                        {
                            try
                            {
                                decodeProgress.Gif = CompleteDecode(parser, decoded);
                                decodeProgress.Completed = true;
							}
                            catch (Exception e)
                            {
                                decodeProgress.Exception = e;
                                decodeProgress.Completed = true;
							}
						}

                        onProgress(decodeProgress);
                    }
                });
			}
		}

		private static readonly Color32 EmptyColor = new Color32();

		private static Gif CompleteDecode(GifParser parser, IDictionary<ImageDescriptor, byte[]> decoded)
		{
			var globalColorTable = parser.LogicalScreenDescriptor.GlobalColorTableFlag == 1 ? GetUnityColors(parser.GlobalColorTable) : null;
			//var backgroundColor = globalColorTable?[parser.LogicalScreenDescriptor.BackgroundColorIndex] ?? EmptyColor;
			GraphicControlExtension gcExtension = null;
			var width = parser.LogicalScreenDescriptor.LogicalScreenWidth;
			var height = parser.LogicalScreenDescriptor.LogicalScreenHeight;
			var state = new Color32[width * height];
			var filled = false;
			var frames = new List<GifFrame>();
			
			for (var j = 0; j < parser.Blocks.Count; j++)
			{
				if (parser.Blocks[j] is GraphicControlExtension)
				{
					gcExtension = (GraphicControlExtension) parser.Blocks[j];
				}
				else if (parser.Blocks[j] is ImageDescriptor)
				{
					var imageDescriptor = (ImageDescriptor) parser.Blocks[j];

					if (imageDescriptor.InterlaceFlag == 1) throw new NotSupportedException("Interlacing is not supported!");

					var colorTable = imageDescriptor.LocalColorTableFlag == 1 ? GetUnityColors((ColorTable) parser.Blocks[j + 1]) : globalColorTable;
					var colorIndexes = decoded[imageDescriptor];
					var frame = DecodeFrame(gcExtension, imageDescriptor, colorIndexes, filled, width, height, state, colorTable);

					frames.Add(frame);

					//if (frames.Count == 1 && globalColorTable != null)
					//{
					//	if (gcExtension == null || gcExtension.TransparentColorFlag == 0 || gcExtension.TransparentColorIndex != parser.LogicalScreenDescriptor.BackgroundColorIndex)
					//	{
					//		backgroundColor = globalColorTable[parser.LogicalScreenDescriptor.BackgroundColorIndex];
					//	}
					//}

					switch (frame.DisposalMethod)
					{
						case DisposalMethod.NoDisposalSpecified:
						case DisposalMethod.DoNotDispose:
							break;
						case DisposalMethod.RestoreToBackgroundColor:
							for (var i = 0; i < state.Length; i++)
							{
								state[i] = EmptyColor;
							}
							filled = true;
							break;
						case DisposalMethod.RestoreToPrevious: // 'state' was already copied before decoding current frame
							filled = false;
							break;
						default:
							throw new NotSupportedException($"Unknown disposal method: {frame.DisposalMethod}!");
					}
				}
			}

			return new Gif(frames);
		}
	
		/// <summary>
		/// Iterator can be used for large GIF-files in order to display progress bar.
		/// </summary>
		public static IEnumerable<GifFrame> DecodeIterator(byte[] bytes)
		{
			var parser = new GifParser(bytes);
			var blocks = parser.Blocks;
			var width = parser.LogicalScreenDescriptor.LogicalScreenWidth;
			var height = parser.LogicalScreenDescriptor.LogicalScreenHeight;
			var globalColorTable = parser.LogicalScreenDescriptor.GlobalColorTableFlag == 1 ? GetUnityColors(parser.GlobalColorTable) : null;
			//var backgroundColor = globalColorTable?[parser.LogicalScreenDescriptor.BackgroundColorIndex] ?? EmptyColor;
			GraphicControlExtension graphicControlExtension = null;
			var state = new Color32[width * height];
			var filled = false;
			
			for (var j = 0; j < parser.Blocks.Count; j++)
			{
				if (blocks[j] is GraphicControlExtension)
				{
					graphicControlExtension = (GraphicControlExtension) blocks[j];
				}
				else if (blocks[j] is ImageDescriptor)
				{
					var imageDescriptor = (ImageDescriptor) blocks[j];

					if (imageDescriptor.InterlaceFlag == 1) throw new NotSupportedException("Interlacing is not supported!");

					var colorTable = imageDescriptor.LocalColorTableFlag == 1 ? GetUnityColors((ColorTable) blocks[j + 1]) : globalColorTable;
					var data = (TableBasedImageData) blocks[j + 1 + imageDescriptor.LocalColorTableFlag];
					var frame = DecodeFrame(graphicControlExtension, imageDescriptor, data, filled, width, height, state, colorTable);

				    if (_free)
				    {
				        if (frame.Texture.width > 256 || frame.Texture.height > 256) throw new Exception("The Free version has maximum supported size 256x256 px. Please consider buying the Full version of Power GIF.");
                        //if (++frames > 20) throw new Exception("The Free version is limited by 20 frames. Please consider buying the Full version of Power GIF.");
                    }

                    yield return frame;

					switch (frame.DisposalMethod)
					{
						case DisposalMethod.NoDisposalSpecified:
						case DisposalMethod.DoNotDispose:
							break;
						case DisposalMethod.RestoreToBackgroundColor:
							for (var i = 0; i < state.Length; i++)
							{
								state[i] = EmptyColor;
							}
							filled = true;
							break;
						case DisposalMethod.RestoreToPrevious: // 'state' was already copied before decoding current frame
							filled = false;
							break;
						default:
							throw new NotSupportedException($"Unknown disposal method: {frame.DisposalMethod}!");
					}
				}
			}
		}

		/// <summary>
		/// Get frame count. Can be used with DecodeIterator to display progress bar.
		/// </summary>
		public static int GetDecodeIteratorSize(byte[] bytes)
		{
			var parser = new GifParser(bytes);

			return parser.Blocks.Count(i => i is ImageDescriptor);
		}

		/// <summary>
		/// Apply master palette to convert true color image to 256-color image.
		/// </summary>
		public Gif ApplyPalette(MasterPalette palette)
		{
		    if (palette == MasterPalette.DontApply) return this;

			var progress = new List<object>();
			var manualResetEvent = new ManualResetEvent(false);

			for (var i = 0; i < Frames.Count; i++)
			{
				var frame = Frames[i];

				ThreadPool.QueueUserWorkItem(context =>
				{
					frame.ApplyPalette(palette);

					lock (progress)
					{
						progress.Add(context);

						if (progress.Count == Frames.Count)
						{
							manualResetEvent.Set();
						}
					}
				}, i);
			}

			manualResetEvent.WaitOne();

			return this;
		}

		/// <summary>
		/// Encode all frames to byte array
		/// </summary>
		public byte[] Encode(int scale = 1)
		{
		    if (_free)
		    {
		        if (Frames[0].Texture.width > 256 || Frames[0].Texture.height > 256) throw new Exception("The free version has maximum supported size 256x256 px. Please consider buying the Full version of Power GIF.");
                if (Frames.Count > 20) throw new Exception("The Free version is limited by 20 frames. Please consider buying the Full version of Power GIF.");
            }

            var bytes = new List<byte>();
			var iterator = EncodeIterator(scale);
			var iteratorSize = GetEncodeIteratorSize();
			var index = 0;

			foreach (var part in iterator)
			{
				if (index == iteratorSize - 1) // GIF header should be placed to sequence start!
				{
					bytes.InsertRange(0, part);
				}
				else
				{
					bytes.AddRange(part);
				}

				index++;
			}

			return bytes.ToArray();
		}

		/// <summary>
		/// Encode GIF in multiple threads.
		/// </summary>
		public void EncodeParallel(Action<EncodeProgress> onProgress, int scale = 1) // TODO: Refact.
		{
		    if (_free)
		    {
		        throw new Exception("The Free version doesn't support this feature. Please consider buying the Full version of Power GIF.");
            }

            const string header = "GIF89a";
			var width = (ushort) (Frames[0].Texture.width * scale);
			var height = (ushort) (Frames[0].Texture.height * scale);
			var globalColorTable = new List<Color32>();
			var applicationExtension = new ApplicationExtension();
			var encoded = new Dictionary<int, List<byte>>();
			var encodeProgress = new EncodeProgress { FrameCount = Frames.Count };
			var colorTables = new List<Color32>[Frames.Count];
			var distinctColors = new Dictionary<int, List<Color32>>();
            var manualResetEvent = new ManualResetEvent(false);

            for (var i = 0; i < Frames.Count; i++)
            {
                var frame = Frames[i];

                ThreadPool.QueueUserWorkItem(context =>
                {
                    var distinct = frame.Texture.GetPixels32().Distinct().ToList();

                    lock (distinctColors)
                    {
                        distinctColors.Add((int)context, distinct);

                        if (distinctColors.Count == Frames.Count) manualResetEvent.Set();
                    }
                }, i);
            }

            manualResetEvent.WaitOne();

			for (var i = 0; i < Frames.Count; i++)
			{
				var colors = distinctColors[i];
				var add = colors.Where(j => !globalColorTable.Contains(j)).ToList();

				if (globalColorTable.Count + add.Count <= 256)
				{
					globalColorTable.AddRange(add);
					colorTables[i] = globalColorTable;
				}
				else if (colors.Count <= 256) // Introduce local color table.
				{
					colorTables[i] = colors;
				}
				else
				{
					onProgress(new EncodeProgress { Completed = true, Exception = new Exception($"Frame #{i} contains more than 256 colors!") });
					return;
				}
			}

			ReplaceTransparentColor(ref globalColorTable);

			for (var i = 0; i < Frames.Count; i++) // Don't use Parallel.For to leave .NET compatibility.
            {
                ThreadPool.QueueUserWorkItem(context =>
                {
                    var index = (int) context;
					var colorTable = colorTables[index];
				    var localColorTableFlag = (byte)(colorTable == globalColorTable ? 0 : 1);
				    var localColorTableSize = GetColorTableSize(colorTable);
				    byte transparentColorFlag = 0, transparentColorIndex = 0;
                    byte max;
                    var colorIndexes = GetColorIndexes(Frames[index].Texture, scale, colorTable, localColorTableFlag, ref transparentColorFlag, ref transparentColorIndex, out max);
				    var graphicControlExtension = new GraphicControlExtension(4, 0, (byte) Frames[index].DisposalMethod, 0, transparentColorFlag, (ushort) (100 * Frames[index].Delay), transparentColorIndex);
				    var imageDescriptor = new ImageDescriptor(0, 0, width, height, localColorTableFlag, 0, 0, 0, localColorTableSize);
				    var minCodeSize = LzwEncoder.GetMinCodeSize(max);
				    var lzw = LzwEncoder.Encode(colorIndexes, minCodeSize);
				    var tableBasedImageData = new TableBasedImageData(minCodeSize, lzw);
				    var bytes = new List<byte>();

				    bytes.AddRange(graphicControlExtension.GetBytes());
				    bytes.AddRange(imageDescriptor.GetBytes());

				    if (localColorTableFlag == 1)
				    {
					    bytes.AddRange(ColorTableToBytes(colorTable, localColorTableSize));
				    }

				    bytes.AddRange(tableBasedImageData.GetBytes());

				    lock (encoded)
				    {
					    encoded.Add(index, bytes);
					    encodeProgress.Progress++;

					    if (encoded.Count == Frames.Count)
					    {
						    var globalColorTableSize = GetColorTableSize(globalColorTable);
						    var logicalScreenDescriptor = new LogicalScreenDescriptor(width, height, 1, 7, 0, globalColorTableSize, 0, 0);
						    var binary = new List<byte>();

						    binary.AddRange(Encoding.UTF8.GetBytes(header));
						    binary.AddRange(logicalScreenDescriptor.GetBytes());
						    binary.AddRange(ColorTableToBytes(globalColorTable, globalColorTableSize));
						    binary.AddRange(applicationExtension.GetBytes());
						    binary.AddRange(encoded.OrderBy(j => j.Key).SelectMany(j => j.Value));
						    binary.Add(0x3B); // GIF Trailer.

						    encodeProgress.Bytes = binary.ToArray();
						    encodeProgress.Completed = true;
					    }

                        onProgress(encodeProgress);
                    }
                }, i);
            }
        }

		/// <summary>
		/// Iterator can be used for large GIF-files in order to display progress bar.
		/// </summary>
		public IEnumerable<List<byte>> EncodeIterator(int scale = 1)
		{
		    if (_free)
		    {
		        if (Frames[0].Texture.width > 256 || Frames[0].Texture.height > 256) throw new Exception("The free version has maximum supported size 256x256 px. Please consider buying the Full version of Power GIF.");
		        if (Frames.Count > 20) throw new Exception("The Free version is limited by 20 frames. Please consider buying the Full version of Power GIF.");
		    }

            const string header = "GIF89a";
			var width = (ushort) (Frames[0].Texture.width * scale);
		    var height = (ushort) (Frames[0].Texture.height * scale);
            var globalColorTable = new List<Color32>();
			var applicationExtension = new ApplicationExtension();
			var bytes = new List<byte>();
			var colorTables = new List<Color32>[Frames.Count];
			var distinctColors = new Dictionary<int, List<Color32>>();
			var manualResetEvent = new ManualResetEvent(false);

			for (var i = 0; i < Frames.Count; i++)
			{
				var frame = Frames[i];

				ThreadPool.QueueUserWorkItem(context =>
				{
					var distinct = frame.Texture.GetPixels32().Distinct().ToList();

					lock (distinctColors)
					{
						distinctColors.Add((int) context, distinct);

						if (distinctColors.Count == Frames.Count) manualResetEvent.Set();
					}
				}, i);
			}

			manualResetEvent.WaitOne();

			for (var i = 0; i < Frames.Count; i++)
			{
				var colors = distinctColors[i];
				var add = colors.Where(j => !globalColorTable.Contains(j)).ToList();

				if (globalColorTable.Count + add.Count <= 256)
				{
					globalColorTable.AddRange(add);
					colorTables[i] = globalColorTable;
				}
				else if (add.Count <= 256) // Introducing local color table
				{
					colorTables[i] = colors;
				}
				else
				{
					throw new Exception($"Frame #{i} contains more than 256 colors!");
				}
			}

			ReplaceTransparentColor(ref globalColorTable);

			for (var i = 0; i < Frames.Count; i++)
			{
				var frame = Frames[i];
				var colorTable = colorTables[i];
				var localColorTableFlag = (byte) (colorTable == globalColorTable ? 0 : 1);
				var localColorTableSize = GetColorTableSize(colorTable);
				byte transparentColorFlag = 0, transparentColorIndex = 0;
                byte max;
				var colorIndexes = GetColorIndexes(frame.Texture, scale, colorTable, localColorTableFlag, ref transparentColorFlag, ref transparentColorIndex, out max);
				var graphicControlExtension = new GraphicControlExtension(4, 0, (byte) frame.DisposalMethod, 0, transparentColorFlag, (ushort) (100 * frame.Delay), transparentColorIndex);
				var imageDescriptor = new ImageDescriptor(0, 0, width, height, localColorTableFlag, 0, 0, 0, localColorTableSize);
				var minCodeSize = LzwEncoder.GetMinCodeSize(max);
				var lzw = LzwEncoder.Encode(colorIndexes, minCodeSize);
				var tableBasedImageData = new TableBasedImageData(minCodeSize, lzw);

				bytes.Clear();
				bytes.AddRange(graphicControlExtension.GetBytes());
				bytes.AddRange(imageDescriptor.GetBytes());

				if (localColorTableFlag == 1)
				{
					bytes.AddRange(ColorTableToBytes(colorTable, localColorTableSize));
				}

				bytes.AddRange(tableBasedImageData.GetBytes());

				yield return bytes;
			}

			yield return new List<byte> { 0x3B }; // GIF Trailer.

			// Then output GIF header as last iterator element! This way we can build global color table "on fly" instead of expensive building operation.

			var globalColorTableSize = GetColorTableSize(globalColorTable);
			var logicalScreenDescriptor = new LogicalScreenDescriptor(width, height, 1, 7, 0, globalColorTableSize, 0, 0);

			bytes.Clear();
			bytes.AddRange(Encoding.UTF8.GetBytes(header));
			bytes.AddRange(logicalScreenDescriptor.GetBytes());
			bytes.AddRange(ColorTableToBytes(globalColorTable, globalColorTableSize));
			bytes.AddRange(applicationExtension.GetBytes());

			yield return bytes;
		}

		/// <summary>
		/// Get parts count for EncodeIterator. Can be used with EncodeIterator to display progress bar.
		/// </summary>
		public int GetEncodeIteratorSize()
		{
			return Frames.Count + 2;
		}

		private static Color32 GetTransparentColor(List<Color32> colorTable)
		{
			for (byte r = 0; r < 0xFF; r++)
			{
				for (byte g = 0; g < 0xFF; g++)
				{
					for (byte b = 0; b < 0xFF; b++)
					{
						var transparentColor = new Color32(r, g, b, 1);

						if (!colorTable.Contains(transparentColor))
						{
							return transparentColor;
						}
					}
				}
			}

			throw new Exception("Unable to resolve transparent color!");
		}

		private static byte[] ColorTableToBytes(List<Color32> colorTable, byte colorTableSize)
		{
			if (colorTable.Count > 256) throw new Exception("Color table size exceeds 256 size limit: " + colorTable.Count);

			var size = 1 << (colorTableSize + 1);
			var bytes = new byte[3 * size];

			for (var i = 0; i < colorTable.Count; i++)
			{
				bytes[3 * i] = colorTable[i].r;
				bytes[3 * i + 1] = colorTable[i].g;
				bytes[3 * i + 2] = colorTable[i].b;
			}

			return bytes;
		}

		private static byte GetColorTableSize(List<Color32> colorTable)
		{
			byte size = 0;

			while (1 << (size + 1) < colorTable.Count)
			{
				size++;
			}

			return size;
		}

		private static byte[] GetColorIndexes(Texture2D texture, int scale, List<Color32> colorTable, byte localColorTableFlag, ref byte transparentColorFlag, ref byte transparentColorIndex, out byte max)
		{
			var indexes = new Dictionary<Color32, int>();

			for (var i = 0; i < colorTable.Count; i++)
			{
				indexes.Add(colorTable[i], i);
			}

			var pixels = texture.GetPixels32();
			var colorIndexes = new byte[pixels.Length * scale * scale];
			
            max = 0;

            Action<int, int, byte> setScaledIndex = (x, y, index) =>
            {
                for (var dy = 0; dy < scale; dy++)
                {
                    for (var dx = 0; dx < scale; dx++)
                    {
                        colorIndexes[x * scale + dx + (y * scale + dy) * texture.width * scale] = index;
                    }
                }
            };

			for (var y = 0; y < texture.height; y++)
			{
				for (var x = 0; x < texture.width; x++)
				{
					var pixel = pixels[x + (texture.height - y - 1) * texture.width];

					if (pixel.a == 0)
					{
						if (transparentColorFlag == 0)
						{
							transparentColorFlag = 1;

							if (localColorTableFlag == 1)
							{
								transparentColorIndex = (byte) indexes[pixel];
								colorTable[transparentColorIndex] = GetTransparentColor(colorTable);
							}
						}

                        if (scale == 1)
                        {
                            colorIndexes[x + y * texture.width] = transparentColorIndex;
						}
                        else
                        {
                            setScaledIndex(x, y, transparentColorIndex);
                        }

                        if (transparentColorIndex > max) max = transparentColorIndex;
					}
					else
					{
						var index = indexes[pixel];

						if (index >= 0)
                        {
                            var i = (byte) index;

							if (scale == 1)
                            {
								colorIndexes[x + y * texture.width] = i;
							}
                            else
                            {
                                setScaledIndex(x, y, i);
                            }

                            if (i > max) max = i;
						}
						else
						{
							throw new Exception("Color index not found: " + pixel);
						}
					}
				}
			}

			return colorIndexes;
		}
        
		private static GifFrame DecodeFrame(GraphicControlExtension extension, ImageDescriptor descriptor, TableBasedImageData data, bool filled, int width, int height, Color32[] state, Color32[] colorTable)
		{
			var colorIndexes = LzwDecoder.Decode(data.ImageData, data.LzwMinimumCodeSize);

			return DecodeFrame(extension, descriptor, colorIndexes, filled, width, height, state, colorTable);
		}

		private static GifFrame DecodeFrame(GraphicControlExtension extension, ImageDescriptor descriptor, byte[] colorIndexes, bool filled, int width, int height, Color32[] state, Color32[] colorTable)
		{
			var frame = new GifFrame();
			var pixels = state;
			var transparentIndex = -1;

			if (extension != null)
			{
				frame.Delay = extension.DelayTime / 100f;
				frame.DisposalMethod = (DisposalMethod) extension.DisposalMethod;

				if (frame.DisposalMethod == DisposalMethod.RestoreToPrevious)
				{
					pixels = state.ToArray();
				}

				if (extension.TransparentColorFlag == 1)
				{
					transparentIndex = extension.TransparentColorIndex;
				}
			}

			for (var y = 0; y < descriptor.ImageHeight; y++)
			{
				for (var x = 0; x < descriptor.ImageWidth; x++)
				{
					var colorIndex = colorIndexes[x + y * descriptor.ImageWidth];
					var transparent = colorIndex == transparentIndex;

					if (transparent && !filled) continue;

					var color = transparent ? EmptyColor : colorTable[colorIndex];
					var fx = x + descriptor.ImageLeftPosition;
					var fy = height - y - 1 - descriptor.ImageTopPosition; // Y-flip

					pixels[fx + fy * width] = pixels[fx + fy * width] = color;
				}
			}

			frame.Texture = new Texture2D(width, height);
			frame.Texture.SetPixels32(pixels);
			frame.Texture.Apply();

			return frame;
		}

		private static Color32[] GetUnityColors(ColorTable table)
		{
			var colors = new Color32[table.Bytes.Length / 3];

			for (var i = 0; i < colors.Length; i++)
			{
				colors[i] = new Color32(table.Bytes[3 * i], table.Bytes[3 * i + 1], table.Bytes[3 * i + 2], 0xFF);
			}

			return colors;
		}

		private static void ReplaceTransparentColor(ref List<Color32> colors)
		{
			for (var i = 0; i < colors.Count; i++)
			{
				if (colors[i].a == 0)
				{
					colors.RemoveAll(j => j.a == 0);
					colors.Insert(0, GetTransparentColor(colors));

					return;
				}
			}
		}
    }
}
namespace SimpleGif.GifCore
{
	/// <summary>
	/// Converter textures.
	/// </summary>
	internal static class TextureConverter
	{
		/// <summary>
		/// /// Apply master palette to convert true color image to 256-color image.
		/// </summary>
		public static void ConvertTo8Bits(ref Texture2D texture, MasterPalette palette)
		{
			if (palette == MasterPalette.DontApply) return;

			var pixels = texture.GetPixels32();

			if (palette == MasterPalette.Grayscale)
			{
				for (var j = 0; j < pixels.Length; j++)
				{
					if (pixels[j].a < 128)
					{
						pixels[j] = new Color32();
					}
					else
					{
						var brightness = (byte) (0.2126 * pixels[j].r + 0.7152 * pixels[j].g + 0.0722 * pixels[j].b);
						var color = new Color32(brightness, brightness, brightness, 255);

						pixels[j] = color;
					}
				}

				texture.SetPixels32(pixels);
			}
			else
			{
				var levels = GetLevels(palette);
				var dividers = new[] { 256 / levels[0], 256 / levels[1], 256 / levels[2] };
				
				for (var j = 0; j < pixels.Length; j++)
				{
					var r = (byte) (pixels[j].r / dividers[0] * dividers[0]);
					var g = (byte) (pixels[j].g / dividers[1] * dividers[1]);
					var b = (byte) (pixels[j].b / dividers[2] * dividers[2]);
					var a = (byte) (pixels[j].a < 128 ? 0 : 255);
					var color = a == 0 ? new Color32() : new Color32(r, g, b, a);

					pixels[j] = color;
				}

				texture.SetPixels32(pixels);
			}
		}

		private static int[] GetLevels(MasterPalette palette)
		{
			switch (palette)
			{
				case MasterPalette.Levels666: return new[] { 6, 6, 6 };
				case MasterPalette.Levels676: return new[] { 6, 7, 6 };
				case MasterPalette.Levels685: return new[] { 6, 8, 5 };
				case MasterPalette.Levels884: return new[] { 8, 8, 4 };
				default: throw new ArgumentOutOfRangeException("Unsupported master palette: " + palette);
			}
		}
	}
}
namespace SimpleGif.GifCore
{
	internal class TableBasedImageData : Block
	{
		public byte LzwMinimumCodeSize;
		public byte[] ImageData;
		public byte BlockTerminator;

		public TableBasedImageData(byte[] bytes, ref int index)
		{
			LzwMinimumCodeSize = bytes[index++];
			ImageData = ReadDataSubBlocks(bytes, ref index);
			BlockTerminator = bytes[index++];

			if (BlockTerminator != 0x00) throw new Exception("0x00 expected!");
		}

		public TableBasedImageData(byte minCodeSize, byte[] imageData)
		{
			LzwMinimumCodeSize = minCodeSize;
			ImageData = imageData;
		}

		public byte[] GetBytes()
		{
			var bytes = new byte[ImageData.Length + (int) Math.Ceiling(ImageData.Length / 255d) + 2];
			var i = 0;
			var j = 0;

			bytes[0] = LzwMinimumCodeSize;
			j++;

			while (i < ImageData.Length)
			{
				var left = ImageData.Length - i;
				var size = (byte) Math.Min(255, left);
				
				bytes[j] = size;
				Array.Copy(ImageData, i, bytes, j + 1, size);
				j += size + 1;
				i += size;
			}

			bytes[bytes.Length - 1] = 0x00;

			return bytes;
		}
	}
}
namespace SimpleGif.GifCore
{
	internal static class LzwEncoder
	{
		public static byte GetMinCodeSize(byte max)
		{
			byte minCodeSize = 2;

			while (1 << minCodeSize <= max)
			{
				minCodeSize++;
			}

			return minCodeSize;
		}

		public static byte[] Encode(byte[] colorIndexes, int minCodeSize)
		{
			var dict = InitializeDictionary(minCodeSize);
			var clearCode = 1 << minCodeSize;
			var endOfInformation = clearCode + 1;
			var code = new[] { colorIndexes[0] };
			var codeSize = minCodeSize + 1;
			var bits = new List<bool>();

			ReadBits(clearCode, codeSize, ref bits);

			for (var i = 1; i < colorIndexes.Length; i++)
			{
				var next = new byte[code.Length + 1];

				Array.Copy(code, next, code.Length);
				next[next.Length - 1] = colorIndexes[i];

				if (dict.ContainsKey(next))
				{
					code = next;
				}
				else
				{
					ReadBits(dict[code], codeSize, ref bits);
					code = new[] { colorIndexes[i] };

					if (dict.Count + 2 < 4096) // + CC + EoF
					{
						dict.Add(next, dict.Count + 2);

						if (dict.Count + 2 - 1 == 1 << codeSize)
						{
							codeSize++;
						}
					}
				}
			}

			ReadBits(dict[code], codeSize, ref bits);
			ReadBits(endOfInformation, codeSize, ref bits);

			var bytes = GetBytes(bits);

			return bytes;
		}

		private static Dictionary<byte[], int> InitializeDictionary(int minCodeSize)
		{
			var dict = new Dictionary<byte[], int>(new ByteArrayComparer());

			for (var i = 0; i < 1 << minCodeSize; i++)
			{
				dict.Add(new[] { (byte) i }, i);
			}

			return dict;
		}

		private static void ReadBits(int key, int codeSize, ref List<bool> destination)
		{
			for (var j = 0; j < codeSize; j++)
			{
				destination.Add(GetBit(key, j));
			}
		}

		private static bool GetBit(int value, int index)
		{
			return (value & (1 << index)) != 0;
		}

		private static byte[] GetBytes(IList<bool> bits)
		{
			var size = bits.Count >> 3;

			if ((bits.Count & 0x07) != 0) ++size;

			var bytes = new byte[size];

			for (var i = 0; i < bits.Count; i++)
			{
				if (bits[i])
				{
					bytes[i >> 3] |= (byte) (1 << (i & 0x07));
				}
			}

			return bytes;
		}
	}
}
namespace SimpleGif.GifCore
{
	namespace Assets.GifCore
	{
		internal static class LzwDecoder
		{
			public static byte[] Decode(byte[] bytes, int minCodeSize)
			{
				var bits = new BitArray(bytes);
				var clearCode = 1 << minCodeSize;
				var endOfInformation = clearCode + 1;
				var codeSize = minCodeSize + 1;
				var dict = InitializeDictionary(minCodeSize);
				var index = codeSize;
				var value = ReadBits(bits, codeSize, ref index);
				var prev = dict[value];
				var colorIndexes = prev.ToList();
				
				while (index + codeSize <= bits.Length)
				{
					value = ReadBits(bits, codeSize, ref index);

					if (value == clearCode)
					{
						codeSize = minCodeSize + 1;
						dict = InitializeDictionary(minCodeSize);
						value = ReadBits(bits, codeSize, ref index);
						colorIndexes.AddRange(prev = dict[value]);
						continue;
					}

					if (value == endOfInformation)
					{
						break;
					}

					if (dict.Count < 4096)
					{
						var code = prev.ToList();

						if (dict.ContainsKey(value))
						{
							code.Add(dict[value][0]);
							dict.Add(dict.Count, code);
						}
						else
						{
							code.Add(prev[0]);
							dict.Add(value, code);
						}

						if (dict.Count == 1 << codeSize && codeSize < 12)
						{
							codeSize++;
						}
					}

					colorIndexes.AddRange(prev = dict[value]);
				}

				return colorIndexes.ToArray();
			}

			private static Dictionary<int, List<byte>> InitializeDictionary(int minCodeSize)
			{
				var dict = new Dictionary<int, List<byte>>();

				for (var i = 0; i < (1 << minCodeSize) + 2; i++)
				{
					dict.Add(i, new List<byte> { (byte) i });
				}

				return dict;
			}

			private static int ReadBits(BitArray bits, int size, ref int cursor) // TODO: Most 'heavy' operation
			{
				var value = 0;

				for (var i = 0; i < size; i++)
				{
					if (bits[cursor + i])
					{
						value += 1 << i;
					}
				}

				cursor += size;

				return value;
			}
		}
	}
}
namespace SimpleGif.GifCore
{
	internal class LogicalScreenDescriptor
	{
		public ushort LogicalScreenWidth;
		public ushort LogicalScreenHeight;
		public byte GlobalColorTableFlag;
		public byte ColorResolution;
		public byte SortFlag;
		public byte GlobalColorTableSize;
		public byte BackgroundColorIndex;
		public byte PixelAspecRatio;

		public LogicalScreenDescriptor(byte[] bytes, ref int index)
		{
			LogicalScreenWidth = BitHelper.ReadInt16(bytes, ref index);
			LogicalScreenHeight = BitHelper.ReadInt16(bytes, ref index);

			GlobalColorTableFlag = BitHelper.ReadPackedByte(bytes[index], 0, 1);
			ColorResolution = BitHelper.ReadPackedByte(bytes[index], 1, 3);
			SortFlag = BitHelper.ReadPackedByte(bytes[index], 4, 1);
			GlobalColorTableSize = BitHelper.ReadPackedByte(bytes[index++], 5, 3);

			BackgroundColorIndex = bytes[index++];
			PixelAspecRatio = bytes[index++];
		}

		public LogicalScreenDescriptor(ushort logicalScreenWidth, ushort logicalScreenHeight,
			byte globalColorTableFlag, byte colorResolution, byte sortFlag, byte gobalColorTableSize, byte backgroundColorIndex, byte pixelAspecRatio)
		{
			LogicalScreenWidth = logicalScreenWidth;
			LogicalScreenHeight = logicalScreenHeight;
			GlobalColorTableFlag = globalColorTableFlag;
			ColorResolution = colorResolution;
			SortFlag = sortFlag;
			GlobalColorTableSize = gobalColorTableSize;
			BackgroundColorIndex = backgroundColorIndex;
			PixelAspecRatio = pixelAspecRatio;
		}

		public List<byte> GetBytes()
		{
			var bytes = new List<byte>();

			bytes.AddRange(BitConverter.GetBytes(LogicalScreenWidth));
			bytes.AddRange(BitConverter.GetBytes(LogicalScreenHeight));

			var packedByte = BitHelper.PackByte(
				GlobalColorTableFlag == 1,
				BitHelper.ReadByte(ColorResolution, 2),
				BitHelper.ReadByte(ColorResolution, 1),
				BitHelper.ReadByte(ColorResolution, 0),
				SortFlag == 1,
				BitHelper.ReadByte(GlobalColorTableSize, 2),
				BitHelper.ReadByte(GlobalColorTableSize, 1),
				BitHelper.ReadByte(GlobalColorTableSize, 0));

			bytes.Add(packedByte);
			bytes.Add(BackgroundColorIndex);
			bytes.Add(PixelAspecRatio);

			return bytes;
		}
	}
}
namespace SimpleGif.GifCore
{
	/// <summary>
	/// Gif specs: https://www.w3.org/Graphics/GIF/spec-gif89a.txt
	/// </summary>
	internal class GifParser
	{
		public string Header;
		public LogicalScreenDescriptor LogicalScreenDescriptor;
		public ColorTable GlobalColorTable;
		public List<Block> Blocks;

		public GifParser(byte[] bytes)
		{
			var index = 6;

			Header = Encoding.UTF8.GetString(bytes, 0, 6);
			LogicalScreenDescriptor = new LogicalScreenDescriptor(bytes, ref index);

			if (LogicalScreenDescriptor.GlobalColorTableFlag == 1)
			{
				GlobalColorTable = new ColorTable(LogicalScreenDescriptor.GlobalColorTableSize, bytes, ref index);
			}

			Blocks = ReadBlocks(bytes, ref index);
		}

		private static List<Block> ReadBlocks(byte[] bytes, ref int startIndex)
		{
			var blocks = new List<Block>();
			var index = startIndex;

			while (true)
			{
				switch (bytes[index])
				{
					case Block.ExtensionIntroducer:
					{
						Block extension;

						switch (bytes[index + 1])
						{
							case Block.PlainTextExtensionLabel:
								extension = new PlainTextExtension(bytes, ref index);
								break;
							case Block.GraphicControlExtensionLabel:
								extension = new GraphicControlExtension(bytes, ref index);
								break;
							case Block.CommentExtensionLabel:
								extension = new CommentExtension(bytes, ref index);
								break;
							case Block.ApplicationExtensionLabel:
								extension = new ApplicationExtension(bytes, ref index);
								break;
							default:
								throw new NotSupportedException("Unknown extension!");
						}

						blocks.Add(extension);
						break;
					}
					case Block.ImageDescriptorLabel:
					{
						var descriptor = new ImageDescriptor(bytes, ref index);

						blocks.Add(descriptor);

						if (descriptor.LocalColorTableFlag == 1)
						{
							var localColorTable = new ColorTable(descriptor.LocalColorTableSize, bytes, ref index);

							blocks.Add(localColorTable);
						}

						var data = new TableBasedImageData(bytes, ref index);

						blocks.Add(data);

						break;
					}
					case 0x3B: // End
					{
						return blocks;
					}
					default:
						throw new NotSupportedException($"Unsupported GIF block: {bytes[index]:X}.");
				}
			}
		}
	}
}
namespace SimpleGif.GifCore
{
	internal sealed class ByteArrayComparer : IEqualityComparer<byte[]>
	{
		public bool Equals(byte[] x, byte[] y)
		{
            if (x.Length != y.Length) return false;

			for (var i = 0; i < x.Length; i++)
			{
				if (x[i] != y[i]) return false;
			}

			return true;
		}

		public int GetHashCode(byte[] array)
		{
			var hash = array.Length;

			for (var i = 0; i < array.Length; i++)
			{
				hash = unchecked(hash * 314159 + array[i]);
			}

			return hash;
		}
    }
}
namespace SimpleGif.GifCore
{
	internal class BitHelper
	{
		public static byte[] ReadBytes(byte[] bytes, int length, ref int index)
		{
			var sequence = new byte[length];

			Array.Copy(bytes, index, sequence, 0, length);
			index += length;

			return sequence;
		}

		public static ushort ReadInt16(byte[] bytes, ref int index)
		{
			var value = (ushort) BitConverter.ToInt16(bytes, index);

			index += 2;

			return value;
		}

		public static byte ReadPackedByte(byte input, int start, int count)
		{
			var shift = 8 - (start + count);
			var mask = (1 << count) - 1;
			var result = (input >> shift) & mask;

			return (byte) result;
		}

		public static byte PackByte(bool bit0, bool bit1, bool bit2, bool bit3, bool bit4, bool bit5, bool bit6, bool bit7)
		{
			byte packedByte = 0;

			if (bit0) packedByte |= 1 << 7;
			if (bit1) packedByte |= 1 << 6;
			if (bit2) packedByte |= 1 << 5;
			if (bit3) packedByte |= 1 << 4;
			if (bit4) packedByte |= 1 << 3;
			if (bit5) packedByte |= 1 << 2;
			if (bit6) packedByte |= 1 << 1;
			if (bit7) packedByte |= 1 << 0;

			return packedByte;
		}

		public static bool ReadByte(byte value, int index)
		{
			return (value & (1 << index)) != 0;
		}
	}
}
namespace SimpleGif.GifCore.Blocks
{
	internal class PlainTextExtension : Block
	{
		public byte BlockSize;
		public ushort TextGridLeftPosition;
		public ushort TextGridTopPosition;
		public ushort TextGridWidth;
		public ushort TextGridHeight;
		public byte CharacterCellWidth;
		public byte CharacterCellHeight;
		public byte TextForegroundColorIndex;
		public byte TextBackgroundColorIndex;
		public byte[] PlainTextData;

		public PlainTextExtension(byte[] bytes, ref int index)
		{
			if (bytes[index++] != ExtensionIntroducer) throw new Exception("Expected :" + ExtensionIntroducer);
			if (bytes[index++] != PlainTextExtensionLabel) throw new Exception("Expected :" + PlainTextExtensionLabel);

			BlockSize = bytes[index++];
			TextGridLeftPosition = BitHelper.ReadInt16(bytes, ref index);
			TextGridTopPosition = BitHelper.ReadInt16(bytes, ref index);
			TextGridWidth = BitHelper.ReadInt16(bytes, ref index);
			TextGridHeight = BitHelper.ReadInt16(bytes, ref index);
			CharacterCellWidth = bytes[index++];
			CharacterCellHeight = bytes[index++];
			TextForegroundColorIndex = bytes[index++];
			TextBackgroundColorIndex = bytes[index++];
			PlainTextData = ReadDataSubBlocks(bytes, ref index);

			if (bytes[index++] != BlockTerminatorLabel) throw new Exception("Expected: " + BlockTerminatorLabel);
		}
	}
}
namespace SimpleGif.GifCore.Blocks
{
	internal class ImageDescriptor : Block
	{
		public ushort ImageLeftPosition;
		public ushort ImageTopPosition;
		public ushort ImageWidth;
		public ushort ImageHeight;
		public byte LocalColorTableFlag;
		public byte InterlaceFlag;
		public byte SortFlag;
		public byte Reserved;
		public byte LocalColorTableSize;

		public ImageDescriptor(byte[] bytes, ref int index)
		{
			if (bytes[index++] != ImageDescriptorLabel) throw new Exception("Expected: " + ImageDescriptorLabel);

			ImageLeftPosition = BitHelper.ReadInt16(bytes, ref index);
			ImageTopPosition = BitHelper.ReadInt16(bytes, ref index);
			ImageWidth = BitHelper.ReadInt16(bytes, ref index);
			ImageHeight = BitHelper.ReadInt16(bytes, ref index);

			LocalColorTableFlag = BitHelper.ReadPackedByte(bytes[index], 0, 1);
			InterlaceFlag = BitHelper.ReadPackedByte(bytes[index], 1, 1);
			SortFlag = BitHelper.ReadPackedByte(bytes[index], 2, 1);
			Reserved = BitHelper.ReadPackedByte(bytes[index], 3, 2);
			LocalColorTableSize = BitHelper.ReadPackedByte(bytes[index++], 5, 3);
		}

		public ImageDescriptor(ushort imageLeftPosition, ushort imageTopPosition, ushort imageWidth, ushort imageHeight,
			byte localColorTableFlag, byte interlaceFlag, byte sortFlag, byte reserved, byte localColorTableSize)
		{
			ImageLeftPosition = imageLeftPosition;
			ImageTopPosition = imageTopPosition;
			ImageWidth = imageWidth;
			ImageHeight = imageHeight;
			LocalColorTableFlag = localColorTableFlag;
			InterlaceFlag = interlaceFlag;
			SortFlag = sortFlag;
			Reserved = reserved;
			LocalColorTableSize = localColorTableSize;
		}

		public List<byte> GetBytes()
		{
			var bytes = new List<byte> { ImageDescriptorLabel };

			bytes.AddRange(BitConverter.GetBytes(ImageLeftPosition));
			bytes.AddRange(BitConverter.GetBytes(ImageTopPosition));
			bytes.AddRange(BitConverter.GetBytes(ImageWidth));
			bytes.AddRange(BitConverter.GetBytes(ImageHeight));

			var packedByte = BitHelper.PackByte(
				LocalColorTableFlag == 1,
				InterlaceFlag == 1,
				SortFlag == 1,
				BitHelper.ReadByte(Reserved, 1),
				BitHelper.ReadByte(Reserved, 0),
				BitHelper.ReadByte(LocalColorTableSize, 2),
				BitHelper.ReadByte(LocalColorTableSize, 1),
				BitHelper.ReadByte(LocalColorTableSize, 0));

			bytes.Add(packedByte);

			return bytes;
		}
	}
}
namespace SimpleGif.GifCore.Blocks
{
	internal class GraphicControlExtension : Block
	{
		public byte BlockSize;
		public byte Reserved;
		public byte DisposalMethod;
		public byte UserInputFlag;
		public byte TransparentColorFlag;
		public ushort DelayTime;
		public byte TransparentColorIndex;

		public GraphicControlExtension(byte[] bytes, ref int index)
		{
			if (bytes[index++] != ExtensionIntroducer) throw new Exception("Expected: " + ExtensionIntroducer);
			if (bytes[index++] != GraphicControlExtensionLabel) throw new Exception("Expected: " + GraphicControlExtensionLabel);
			
			BlockSize = bytes[index++];

			Reserved = BitHelper.ReadPackedByte(bytes[index], 0, 3);
			DisposalMethod = BitHelper.ReadPackedByte(bytes[index], 3, 3);
			UserInputFlag = BitHelper.ReadPackedByte(bytes[index], 6, 1);
			TransparentColorFlag = BitHelper.ReadPackedByte(bytes[index++], 7, 1);

			DelayTime = BitHelper.ReadInt16(bytes, ref index);
			TransparentColorIndex = bytes[index++];

			if (bytes[index++] != BlockTerminatorLabel) throw new Exception("Expected: " + BlockTerminatorLabel);
		}

		public GraphicControlExtension(byte blockSize, byte reserved, byte disposalMethod, byte userInputFlag, byte transparentColorFlag, ushort delayTime, byte transparentColorIndex)
		{
			BlockSize = blockSize;
			Reserved = reserved;
			DisposalMethod = disposalMethod;
			UserInputFlag = userInputFlag;
			TransparentColorFlag = transparentColorFlag;
			DelayTime = delayTime;
			TransparentColorIndex = transparentColorIndex;
		}

		public List<byte> GetBytes()
		{
			var bytes = new List<byte> { ExtensionIntroducer, GraphicControlExtensionLabel, BlockSize };
			var packedByte = BitHelper.PackByte(
				BitHelper.ReadByte(Reserved, 2),
				BitHelper.ReadByte(Reserved, 1),
				BitHelper.ReadByte(Reserved, 0),
				BitHelper.ReadByte(DisposalMethod, 2),
				BitHelper.ReadByte(DisposalMethod, 1),
				BitHelper.ReadByte(DisposalMethod, 0),
				UserInputFlag == 1,
				TransparentColorFlag == 1);

			bytes.Add(packedByte);
			bytes.AddRange(BitConverter.GetBytes(DelayTime));
			bytes.Add(TransparentColorIndex);
			bytes.Add(BlockTerminatorLabel);

			return bytes;
		}
	}
}
namespace SimpleGif.GifCore.Blocks
{
	internal class CommentExtension : Block
	{
		public byte[] CommentData;

		public CommentExtension(byte[] bytes, ref int index)
		{
			if (bytes[index++] != ExtensionIntroducer) throw new Exception("Expected: " + ExtensionIntroducer);
			if (bytes[index++] != CommentExtensionLabel) throw new Exception("Expected: " + CommentExtensionLabel);

			CommentData = ReadDataSubBlocks(bytes, ref index);

			if (bytes[index++] != BlockTerminatorLabel) throw new Exception("Expected: " + BlockTerminatorLabel);
		}
	}
}
namespace SimpleGif.GifCore.Blocks
{
	internal class ColorTable : Block
	{
		public byte[] Bytes;

		public ColorTable(int size, byte[] bytes, ref int index)
		{
			var length = 3 * (int) Math.Pow(2, size + 1);

			Bytes = BitHelper.ReadBytes(bytes, length, ref index);
		}
	}
}
namespace SimpleGif.GifCore.Blocks
{
	internal abstract class Block
	{
		public const byte ExtensionIntroducer = 0x21;
		public const byte PlainTextExtensionLabel = 0x1;
		public const byte GraphicControlExtensionLabel = 0xF9;
		public const byte CommentExtensionLabel = 0xFE;
		public const byte ImageDescriptorLabel = 0x2C;
		public const byte ApplicationExtensionLabel = 0xFF;
		public const byte BlockTerminatorLabel = 0x00;

		protected byte[] ReadDataSubBlocks(byte[] bytes, ref int index)
		{
			var data = new List<byte>();

			while (bytes[index] > 0) // Sub-block size
			{
				var subBlock = BitHelper.ReadBytes(bytes, bytes[index++], ref index);

				if (data.Count == 0)
				{
					data = subBlock.ToList();
				}
				else
				{
				    data.AddRange(subBlock);
                }
			}

			return data.ToArray();
		}
	}
}
namespace SimpleGif.GifCore.Blocks
{
	internal class ApplicationExtension : Block
	{
		public byte BlockSize;
		public byte[] ApplicationIdentifier;
		public byte[] ApplicationAuthenticationCode;
		public byte[] ApplicationData;

		public ApplicationExtension(byte[] bytes, ref int index)
		{
			if (bytes[index++] != ExtensionIntroducer) throw new Exception("Expected: " + ExtensionIntroducer);
			if (bytes[index++] != ApplicationExtensionLabel) throw new Exception("Expected: " + ApplicationExtensionLabel);

			BlockSize = bytes[index++];
			ApplicationIdentifier = BitHelper.ReadBytes(bytes, 8, ref index);
			ApplicationAuthenticationCode = BitHelper.ReadBytes(bytes, 3, ref index);
			ApplicationData = ReadDataSubBlocks(bytes, ref index);

			if (bytes[index++] != BlockTerminatorLabel) throw new Exception("Expected: " + BlockTerminatorLabel);
		}

		public ApplicationExtension()
		{
		}

		public byte[] GetBytes()
		{
			return new byte[] { 0x21, 0xFF, 0x0B, 0x4E, 0x45, 0x54, 0x53, 0x43, 0x41, 0x50, 0x45, 0x32, 0x2E, 0x30, 0x03, 0x01, 0x00, 0x00, 0x00 };
		}
	}
}
