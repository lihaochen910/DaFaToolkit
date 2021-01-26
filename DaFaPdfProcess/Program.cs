using System;
using System.IO;
using System.Text;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;


namespace DaFaPdfProcess {
	
	public static class Program {

		public const string TWO_IN_ONE = "2in1";
		public const string FOUR_IN_ONE = "4in1";
		public const string GB = "gb";
		public const string BIG5 = "big5";
		public const string EXT = ".pdf";
		
		public static void Main ( string[] args ) {

			if ( args.Length < 2 ) {
				Log ( "input: [gb/big5] [pdf/pdfFolder]" );
				return;
			}
			
			Encoding.RegisterProvider ( CodePagesEncodingProvider.Instance );

			bool isFile = true;
			
			FileAttributes attr = File.GetAttributes( args[ 1 ] );

			if ( ( attr & FileAttributes.Directory ) == FileAttributes.Directory ) {
				isFile = false;
			}

			Action < string, string > processFile = ( type, path ) => {
				switch ( type ) {
					case GB:
						ConvertSideVersion ( path ); break;
					case BIG5:
						ConvertBig5Version ( path ); break;
					default:
						break;
				}
			};

			if ( isFile ) {
				processFile ( args[ 0 ], args[ 1 ] );
			}
			else {
				foreach ( var filePath in Directory.EnumerateFiles ( args[ 1 ] ) ) {
					if ( Path.GetExtension ( filePath ).ToLower () == EXT ) {
						processFile ( args[ 0 ], filePath );
					}
				}
			}
		}

		// Support 2in1 and 4in1
		public static void ConvertSideVersion ( string path ) {
			if ( !File.Exists ( path ) ) {
				return;
			}
			
			if ( Path.GetExtension ( path ).ToLower () != EXT ) {
				Log ( $"{path} not a pdf document." );
				return;
			}

			bool is_2in1 = false;
			string filename = null;
			string ext      = null;
			ext     = Path.GetExtension ( path );
			filename = Path.GetFileNameWithoutExtension ( path );

			if ( filename.Contains ( TWO_IN_ONE ) ) {
				is_2in1 = true;
			}
			else {
				if ( !filename.Contains ( FOUR_IN_ONE ) ) {
					Log ( $"只支持2in1/4in1格式的文档 {path}" );
					return;
				}
			}
			
			// var dfDoc = PdfReader.Open ( args[ 0 ] , PdfDocumentOpenMode.Import );

			XGraphics gfx;
			XRect     box;
			XPdfForm  form = XPdfForm.FromFile ( path );
			
			PdfDocument outputDoc = new PdfDocument ();
			outputDoc.Info.Title = form.Page.Owner.Info.Title;
			
			for ( int i = 0; i < form.PageCount; i++ ) {
				// 奇数
				if ( i % 2 != 0 ) {
					// 右边页
					form.PageIndex = form.PageCount - i - 1;
					
					PdfPage outPage = outputDoc.AddPage ();
					outPage.Orientation = PageOrientation.Portrait;
					
					var    page   = form.Page;
					double width  = is_2in1 ? page.Width * 1.35D : page.Width * 2;
					double height = is_2in1 ? page.Height * 1.35D : page.Height * 2;
					box = new XRect ( 0, 0, width, height );
					
					gfx = XGraphics.FromPdfPage ( outPage );
					gfx.TranslateTransform ( is_2in1 ? -page.Width * 0.65D : -page.Width, 0 );
					gfx.DrawImage ( form, box );
					gfx.Dispose ();
					
					// Log ( $"save page: {form.PageNumber} to douDoc" );
				}
				else {
					
					// 左边页
					form.PageIndex = i;
					
					PdfPage outPage = outputDoc.AddPage ();
					outPage.Orientation = PageOrientation.Portrait;
					
					var    page   = form.Page;
					double width  = is_2in1 ? page.Width * 1.35D : page.Width * 2;
					double height = is_2in1 ? page.Height * 1.35D : page.Height * 2;
					box = new XRect ( 0, 0, width, height );
					
					gfx = XGraphics.FromPdfPage ( outPage );
					gfx.DrawImage ( form, box );
					gfx.Dispose ();
					
					// Log ( $"save page: {form.PageNumber} to douDoc" );
				}
			}
			
			outputDoc.Save ( filename + ext );

			if ( is_2in1 ) {
				Log ( $"output {TWO_IN_ONE} {filename + ext}" );
			}
			else {
				Log ( $"output {FOUR_IN_ONE} {filename + ext}" );
			}
		}

		public static void ConvertSideVersion2 ( string path ) {
			if ( !File.Exists ( path ) ) {
				return;
			}
			
			if ( Path.GetExtension ( path ).ToLower () != EXT ) {
				Log ( $"{path} not a pdf document." );
				return;
			}

			bool is_2in1 = false;
			string filename = null;
			string ext      = null;
			ext     = Path.GetExtension ( path );
			filename = Path.GetFileNameWithoutExtension ( path );

			if ( filename.Contains ( TWO_IN_ONE ) ) {
				is_2in1 = true;
			}
			else {
				if ( !filename.Contains ( FOUR_IN_ONE ) ) {
					Log ( $"只支持2in1/4in1格式的文档 {path}" );
					return;
				}
			}
			
			XGraphics gfx;
			XRect     box;
			XPdfForm  form = XPdfForm.FromFile ( path );
			PdfDocument inDoc = PdfReader.Open ( path, PdfDocumentOpenMode.Import );
			
			PdfDocument outputDoc = new PdfDocument ();
			outputDoc.Info.Title = inDoc.Info.Title;
			outputDoc.Info.Creator = inDoc.Info.Creator;
			outputDoc.Info.CreationDate = inDoc.Info.CreationDate;
			
			for ( int i = 0; i < form.PageCount; i++ ) {
				// 奇数
				if ( i % 2 != 0 ) {
					// 右边页
					form.PageIndex = form.PageCount - i - 1;
					
					// PdfPage outPage = outputDoc.AddPage ();
					// outPage.Orientation = PageOrientation.Portrait;
					//
					// var    page   = form.Page;
					// double width  = is_2in1 ? page.Width * 1.35D : page.Width * 2;
					// double height = is_2in1 ? page.Height * 1.35D : page.Height * 2;
					// box = new XRect ( 0, 0, width, height );
					//
					// gfx = XGraphics.FromPdfPage ( outPage );
					// gfx.TranslateTransform ( is_2in1 ? -page.Width * 0.65D : -page.Width, 0 );
					// gfx.DrawImage ( form, box );
					// gfx.Dispose ();
					
					PdfPage outPageRight = outputDoc.AddPage(inDoc.Pages[i]);
					outPageRight.Orientation = PageOrientation.Portrait;
					outPageRight.CropBox = new PdfRectangle(new XRect(0, outPageRight.Height / 2, outPageRight.Width, outPageRight.Height / 2));
					
					// Log ( $"save page: {form.PageNumber} to douDoc" );
				}
				else {
					
					// 左边页
					// form.PageIndex = i;
					//
					// PdfPage outPage = outputDoc.AddPage ();
					// outPage.Orientation = PageOrientation.Portrait;
					//
					// var    page   = form.Page;
					// double width  = is_2in1 ? page.Width * 1.35D : page.Width * 2;
					// double height = is_2in1 ? page.Height * 1.35D : page.Height * 2;
					// box = new XRect ( 0, 0, width, height );
					//
					// gfx = XGraphics.FromPdfPage ( outPage );
					// gfx.DrawImage ( form, box );
					// gfx.Dispose ();
					
					PdfPage outPageLeft = outputDoc.AddPage(inDoc.Pages[i]);
					outPageLeft.Orientation = PageOrientation.Portrait;
					outPageLeft.CropBox = new PdfRectangle(new XRect(0, 0, outPageLeft.Width, outPageLeft.Height / 2));

					
					// Log ( $"save page: {form.PageNumber} to douDoc" );
				}
			}
			
			outputDoc.Save ( filename + ext );

			if ( is_2in1 ) {
				Log ( $"output {TWO_IN_ONE} {filename + ext}" );
			}
			else {
				Log ( $"output {FOUR_IN_ONE} {filename + ext}" );
			}
		}
		
		// Support Big5
		public static void ConvertBig5Version ( string path ) {
			if ( !File.Exists ( path ) ) {
				return;
			}
			
			if ( Path.GetExtension ( path ).ToLower () != EXT ) {
				Log ( $"{path} not a pdf document." );
				return;
			}
			
			string filename = null;
			string ext      = null;
			ext     = Path.GetExtension ( path );
			filename = Path.GetFileNameWithoutExtension ( path );
			
			PdfDocument inDoc = PdfReader.Open ( path, PdfDocumentOpenMode.Import );
			
			PdfDocument outputDoc = new PdfDocument ();
			outputDoc.Info.Title = inDoc.Info.Title;
			outputDoc.Info.Creator = inDoc.Info.Creator;
			outputDoc.Info.CreationDate = inDoc.Info.CreationDate;

			for ( int i = 0; i < inDoc.PageCount; i++ ) {
				
				PdfPage outPageRight = outputDoc.AddPage(inDoc.Pages[i]);
				outPageRight.Size = PageSize.A4;
				outPageRight.Orientation = PageOrientation.Portrait;
				outPageRight.CropBox = new PdfRectangle(new XRect(0, outPageRight.Height / 2, outPageRight.Width, outPageRight.Height / 2));
				
				PdfPage outPageLeft = outputDoc.AddPage(inDoc.Pages[i]);
				outPageRight.Size = PageSize.A4;
				outPageLeft.Orientation = PageOrientation.Portrait;
				outPageLeft.CropBox = new PdfRectangle(new XRect(0, 0, outPageLeft.Width, outPageLeft.Height / 2));

			}
			
			outputDoc.Save ( filename + ext );

			Log ( $"output {BIG5} {filename + ext}" );
		}

		public static void Log ( string str ) {
			Console.WriteLine ( str );
		}
		
	}
}
