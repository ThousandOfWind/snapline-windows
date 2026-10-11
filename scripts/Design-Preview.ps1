$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$output = Join-Path $root 'docs/design'
New-Item -ItemType Directory -Force $output | Out-Null
# Static concept art only: no app launch, desktop capture, clipboard or input automation.
Add-Type -ReferencedAssemblies System.Drawing.Common, System.Drawing.Primitives, System.Private.Windows.GdiPlus, System.Private.Windows.Core @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
public static class SnaplineDesignPreview {
    static Color C(string value) => ColorTranslator.FromHtml(value);
    static GraphicsPath Shape(float x,float y,float w,float h,float r) {
        var p = new GraphicsPath(); float d=r*2;
        p.AddArc(x,y,d,d,180,90);p.AddArc(x+w-d,y,d,d,270,90);
        p.AddArc(x+w-d,y+h-d,d,d,0,90);p.AddArc(x,y+h-d,d,d,90,90);p.CloseFigure();return p;
    }
    static void Box(Graphics g,float x,float y,float w,float h,float r,Color color) {
        using var path=Shape(x,y,w,h,r);using var b=new SolidBrush(color);g.FillPath(b,path);
    }
    static void Outline(Graphics g,float x,float y,float w,float h,float r,Color color,float width=1) {
        using var path=Shape(x,y,w,h,r);using var p=new Pen(color,width);g.DrawPath(p,path);
    }
    static void Text(Graphics g,string text,float x,float y,float size,string hex,bool bold=false) {
        using var font=new Font("Microsoft YaHei UI",size,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel);
        using var brush=new SolidBrush(C(hex));g.DrawString(text,font,brush,x,y);
    }
    static void Line(Graphics g,float x,float y,float tx,float ty,string color,float width=2) {
        using var pen=new Pen(C(color),width){StartCap=LineCap.Round,EndCap=LineCap.Round};g.DrawLine(pen,x,y,tx,ty);
    }
    static void Shadow(Graphics g,float x,float y,float w,float h,float r) {
        for(int i=8;i>0;i--) Box(g,x-i,y+3-i,w+i*2,h+i*2,r+i,Color.FromArgb(2,10,36,32));
    }
    static void Rope(Graphics g,float x,float y,float width,bool dark=false) {
        using var pen=new Pen(C(dark?"#9EBAAD":"#59776C"),2){StartCap=LineCap.Round,EndCap=LineCap.Round};
        g.DrawBezier(pen,x,y,x+width/3,y+7,x+width*2/3,y+7,x+width,y);
    }
    static void Peg(Graphics g,float x,float y,float scale=1) {
        Box(g,x,y,12*scale,27*scale,4*scale,C("#EAAF75"));
        Line(g,x+3*scale,y+10*scale,x+9*scale,y+10*scale,"#A76B42",1.3f*scale);
        Line(g,x+6*scale,y+15*scale,x+6*scale,y+24*scale,"#C78B54",1*scale);
    }
    static void Logo(Graphics g,float x,float y,float size) {
        var state=g.Save();g.TranslateTransform(x,y);g.ScaleTransform(size/100,size/100);
        Rope(g,4,20,92);
        var paper=g.Save();g.TranslateTransform(49,20);g.RotateTransform(-7);
        Shadow(g,-31,9,62,65,9);Box(g,-31,9,62,65,9,C("#F9FCF6"));
        Box(g,-24,17,48,39,5,C("#B5DBCC"));
        Line(g,-16,28,12,28,"#438374",4);Line(g,-16,38,3,38,"#438374",4);
        Line(g,-15,64,9,64,"#ABC6BB",3);
        g.Restore(paper);Peg(g,43,8,1.15f);g.Restore(state);
    }
    static void CaptureIcon(Graphics g,float x,float y,string color) {
        Line(g,x,y+7,x,y,color);Line(g,x,y,x+7,y,color);
        Line(g,x+18,y+7,x+18,y,color);Line(g,x+18,y,x+11,y,color);
        Line(g,x,y+11,x,y+18,color);Line(g,x,y+18,x+7,y+18,color);
        Line(g,x+18,y+11,x+18,y+18,color);Line(g,x+18,y+18,x+11,y+18,color);
    }
    static void Action(Graphics g,float x,float y,string text,bool primary=false,bool hover=false,bool pressed=false) {
        var fill=primary?(pressed?"#94C4B1":hover?"#CAE6D6":"#DAEDDF"):(hover?"#FFFFFF":"#F3F6F0");
        Shadow(g,x,y,92,36,13);Box(g,x,y,92,36,13,C(fill));
        Outline(g,x,y,92,36,13,C(primary?"#B2D3BD":"#D4DFD4"));
        if(primary) CaptureIcon(g,x+13,y+9,"#355C4C");
        else {Line(g,x+18,y+22,x+24,y+16,"#5B6D61");Line(g,x+24,y+16,x+30,y+22,"#5B6D61");}
        Text(g,text,x+39,y+7,15,"#355748",true);
    }
    static void Photo(Graphics g,float center,float top,float angle,int variant) {
        var state=g.Save();g.TranslateTransform(center,top);g.RotateTransform(angle);
        Shadow(g,-79,14,158,141,10);Box(g,-79,14,158,141,10,C("#F9FCF6"));
        Box(g,-71,22,142,104,5,C(variant==0?"#E6EEE3":variant==1?"#C6DDD4":"#EBD8C2"));
        if(variant==0) {
            Box(g,-61,33,52,7,3,C("#A7C3B5"));Box(g,-61,51,114,38,5,C("#FAFCF8"));
            Line(g,-50,62,30,62,"#A4BDB0",3);Line(g,-50,74,10,74,"#BACCC1",3);
            Box(g,-61,101,70,8,3,C("#ABC1B0"));
        } else if(variant==1) {
            using var a=new SolidBrush(C("#79A897"));g.FillEllipse(a,-51,45,45,45);
            Box(g,5,42,54,54,6,C("#E6F1E8"));Line(g,-50,111,49,111,"#91B3A5",4);
        } else {
            Text(g,"a small idea",-52,47,15,"#8E6D51",true);
            Line(g,-52,82,45,82,"#C8AA8C",3);Line(g,-52,96,24,96,"#C8AA8C",3);
        }
        Text(g,"截图 "+(variant+1),-61,132,11,"#70847A");
        // Unobtrusive remove button, visible affordance rather than a square grey X.
        Box(g,58,18,17,17,8,C("#ECF2E9"));Line(g,64,24,69,29,"#8A9B8D",1);Line(g,69,24,64,29,"#8A9B8D",1);
        g.Restore(state);Peg(g,center-6,top,1);
    }
    static void Handle(Graphics g,float x,float y,bool dark=false) {
        // A small photo pendant replaces the old blocky word-button; number is on a paper tab.
        Rope(g,x-8,y+11,100,dark);
        var state=g.Save();g.TranslateTransform(x+28,y+11);g.RotateTransform(-6);
        Shadow(g,-24,7,48,45,7);Box(g,-24,7,48,45,7,C("#F9FCF6"));
        Box(g,-18,13,36,23,4,C("#B6D9C9"));
        Line(g,-11,21,9,21,"#4B8671",2);Line(g,-11,28,3,28,"#4B8671",2);
        g.Restore(state);Peg(g,x+23,y,0.85f);
        Shadow(g,x+59,y+22,44,25,11);Box(g,x+59,y+22,44,25,11,C("#F3F7EE"));
        Text(g,"6 张",x+67,y+26,11,"#547161");
    }
    static void Desktop(Graphics g,float x,float y,float w,float h,bool dark) {
        Box(g,x,y,w,h,20,C(dark?"#23423E":"#E7ECE1"));
        var clip=g.Save();using(var path=Shape(x,y,w,h,20))g.SetClip(path);
        using var p=new SolidBrush(Color.FromArgb(dark?35:60,139,185,166));
        g.FillEllipse(p,x+w*0.42f,y-90,w*0.7f,h*1.9f);
        using var p2=new SolidBrush(Color.FromArgb(dark?25:80,219,206,174));g.FillEllipse(p2,x-190,y+h*0.45f,w*0.7f,h);
        g.Restore(clip);
    }
    public static void Draw(string destination) {
        using var bitmap=new Bitmap(1440,1110);using var g=Graphics.FromImage(bitmap);
        g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        g.Clear(C("#F5F5EE"));
        Text(g,"SNAPLINE  /  DESIGN STUDY",52,32,12,"#758D7E",true);
        Text(g,"像小物件，不像工具栏。",50,60,36,"#243F34",true);
        Text(g,"保留细绳、夹子和照片的轻盈感。统一 Logo、收起入口、按钮与动作。",53,118,16,"#718374");
        // Identity section
        Box(g,50,170,395,270,22,C("#E8EEE3"));Logo(g,79,209,138);
        Text(g,"一张刚挂好的截图",233,229,20,"#2E5140",true);
        Text(g,"薄荷卡片 / 杏橙夹子",233,271,13,"#708473");
        Text(g,"略微倾斜，有重量感",233,300,13,"#708473");
        Logo(g,256,337,50);Logo(g,326,350,30);
        Text(g,"托盘需单独简化",233,400,11,"#879987");
        // Collapsed entry section, two contexts
        Box(g,465,170,925,270,22,C("#FFFFFF"));Text(g,"收起入口 · 一张小照片挂着数量标签",490,191,19,"#2E5140",true);
        Desktop(g,490,237,430,130,false);Desktop(g,938,237,427,130,true);
        Handle(g,645,270);Handle(g,1090,270,true);
        Text(g,"点击展开",506,384,13,"#597562",true);Text(g,"按住照片拖动位置",640,384,13,"#708473");
        Text(g,"文字说明在悬停时出现，不挤进小入口",988,384,12,"#708473");
        // Expanded context
        Text(g,"展开 · 依然是你的晾衣绳",53,476,22,"#2E5140",true);
        Desktop(g,50,524,1340,284,false);
        Logo(g,77,543,32);Text(g,"Snapline",119,551,17,"#436651",true);
        Text(g,"拖动这里移动",216,556,11,"#859481");
        Action(g,1159,544,"截图",true);Action(g,1265,544,"收起");
        Rope(g,78,611,1284);
        Photo(g,428,607,-3,0);Photo(g,713,612,2,1);Photo(g,994,608,-2,2);
        Text(g,"透明区域不铺底板 · 这里只用合成背景示意",74,778,11,"#879987");
        // Button states
        Text(g,"按钮 · 清楚、柔和、有反馈",53,845,21,"#2E5140",true);
        Box(g,50,890,640,170,20,C("#E8EEE3"));
        Action(g,79,920,"截图",true);Action(g,195,920,"截图",true,true);Action(g,311,920,"截图",true,false,true);Action(g,500,920,"收起");
        Text(g,"默认",106,973,12,"#718374");Text(g,"悬停轻亮",208,973,12,"#718374");Text(g,"按下微沉",324,973,12,"#718374");Text(g,"次要操作",514,973,12,"#718374");
        Text(g,"图标线条统一 · 小圆角纸片 · 不再使用厚重深色按钮",78,1018,12,"#7A8E7D");
        // Motion notes: state transitions, not an animation promise.
        Box(g,711,890,679,170,20,C("#FFFFFF"));
        Text(g,"动作来自物理感，不来自炫技",739,909,18,"#2E5140",true);
        Text(g,"新截图：靠近绳子 → 夹住 → 摆两下停稳",739,949,14,"#6D8273");
        Text(g,"复制：夹子轻亮一下    收起：照片向入口轻轻退场",739,980,14,"#6D8273");
        Text(g,"默认安静 · 小幅衰减 · 不影响点击或结果可见性",739,1020,12,"#8A9B8D");
        bitmap.Save(destination,ImageFormat.Png);
    }
}
'@
[SnaplineDesignPreview]::Draw((Join-Path $output 'snapline-concept.png'))
Write-Host "Static concept: $output/snapline-concept.png"
