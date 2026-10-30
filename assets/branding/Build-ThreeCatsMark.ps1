param([string]$OutputImage=(Join-Path $PSScriptRoot 'three-cats-mark.png'))
# Original, geometry-only cat and keyboard mark; no external artwork or font.
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $OutputImage){throw 'Refusing to overwrite an existing brand asset.'}
Add-Type -AssemblyName System.Drawing
$bitmap=[Drawing.Bitmap]::new(1024,1024,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g=[Drawing.Graphics]::FromImage($bitmap)
$head=[Drawing.Drawing2D.GraphicsPath]::new()
$white=[Drawing.SolidBrush]::new([Drawing.Color]::White)
$accent=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(214,35,85))
try {
 $g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
 $g.Clear([Drawing.Color]::FromArgb(214,35,85))
 # One stylized cat face and three separate keys, independent of console marks.
 $head.StartFigure()
 $head.AddLine(254,428,286,175)
 $head.AddLine(286,175,421,276)
 $head.AddBezier(421,276,477,260,547,260,603,276)
 $head.AddLine(603,276,738,175)
 $head.AddLine(738,175,770,428)
 $head.AddBezier(770,428,805,605,681,715,512,715)
 $head.AddBezier(512,715,343,715,219,605,254,428)
 $head.CloseFigure()
 $g.FillPath($white,$head)
 $g.FillEllipse($accent,354,424,64,80);$g.FillEllipse($accent,606,424,64,80)
 $g.FillEllipse($accent,481,539,62,45)
 foreach($x in @(269,429,589)){
  $key=[Drawing.Drawing2D.GraphicsPath]::new()
  try { $d=40;$w=126;$y=785;$h=112
   $key.AddArc($x,$y,$d,$d,180,90);$key.AddArc($x+$w-$d,$y,$d,$d,270,90)
   $key.AddArc($x+$w-$d,$y+$h-$d,$d,$d,0,90);$key.AddArc($x,$y+$h-$d,$d,$d,90,90)
   $key.CloseFigure();$g.FillPath($white,$key)
  } finally {$key.Dispose()}
 }
 $bitmap.Save([IO.Path]::GetFullPath($OutputImage),[Drawing.Imaging.ImageFormat]::Png)
} finally {$accent.Dispose();$white.Dispose();$head.Dispose();$g.Dispose();$bitmap.Dispose()}
Write-Output ('Created original brand mark: '+[IO.Path]::GetFileName($OutputImage))
