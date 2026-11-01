using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Lumina {
    // Popup chrome is themed explicitly: the Windows MenuItem template paints
    // submenu backgrounds with SystemColors even when the item text is themed.
    public sealed class MenuTheme {
        static readonly Dictionary<bool,MenuTheme> themes=new Dictionary<bool,MenuTheme>();
        readonly Style menu,item,separator;
        MenuTheme(bool light){
            // Token replacement keeps both templates on one palette.
            string background=light?"#EEF7FAFF":"#ED253241",ink=light?"#202635":"#F4F6FC",line=light?"#408396AC":"#35D5E3FF",hover=light?"#26739DDC":"#285F9DDD";
            Func<string,string> palette=x=>x.Replace("@BG",background).Replace("@INK",ink).Replace("@LINE",line).Replace("@HOVER",hover).Replace("@MATERIAL",light?"1":"0").Replace("@ASSEMBLY",typeof(PopupChrome).Assembly.GetName().Name);
            string itemXaml=@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:glass='clr-namespace:Lumina;assembly=@ASSEMBLY' TargetType='MenuItem'>
<Grid>
 <Border x:Name='Chrome' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' Background='{TemplateBinding Background}' CornerRadius='8' BorderBrush='Transparent' BorderThickness='1' Padding='{TemplateBinding Padding}'>
  <Grid>
   <Grid.ColumnDefinitions><ColumnDefinition Width='20'/><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto' SharedSizeGroup='Gesture'/><ColumnDefinition Width='12'/></Grid.ColumnDefinitions>
   <ContentPresenter x:Name='Icon' ContentSource='Icon' VerticalAlignment='Center' HorizontalAlignment='Center'/>
   <TextBlock x:Name='Check' Text='✓' Foreground='{TemplateBinding Foreground}' Visibility='Collapsed' VerticalAlignment='Center'/>
   <ContentPresenter Grid.Column='1' ContentSource='Header' RecognizesAccessKey='True' VerticalAlignment='Center' HorizontalAlignment='Left'/>
   <TextBlock Grid.Column='2' Text='{TemplateBinding InputGestureText}' Foreground='{TemplateBinding Foreground}' Opacity='.55' FontSize='11' Margin='24,0,6,0' VerticalAlignment='Center'/>
   <Path x:Name='Arrow' Grid.Column='3' Data='M 0,0 L 4,4 L 0,8' Stroke='{TemplateBinding Foreground}' StrokeThickness='1.6' Visibility='Collapsed' VerticalAlignment='Center' HorizontalAlignment='Right'/>
  </Grid>
 </Border>
 <Popup x:Name='PART_Popup' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' Placement='Right' HorizontalOffset='-7' AllowsTransparency='True' Focusable='False' IsOpen='{Binding IsSubmenuOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'>
<Grid Margin='8'><Border BorderBrush='#60000000' BorderThickness='1' CornerRadius='14' IsHitTestVisible='False'><Border.Effect><DropShadowEffect BlurRadius='14' ShadowDepth='3' Opacity='.22'/></Border.Effect></Border><Border x:Name='SubmenuSurface' Background='@BG' glass:PopupChrome.Material='@MATERIAL' BorderBrush='@LINE' BorderThickness='1' CornerRadius='14' Padding='6' TextElement.Foreground='@INK' SnapsToDevicePixels='True'>
   <ScrollViewer CanContentScroll='False' MaxHeight='420' VerticalScrollBarVisibility='Auto' HorizontalScrollBarVisibility='Disabled'>
    <ItemsPresenter Grid.IsSharedSizeScope='True'/>
   </ScrollViewer>
  </Border></Grid>
 </Popup>
</Grid>
<ControlTemplate.Triggers>
 <Trigger Property='HasItems' Value='True'><Setter TargetName='Arrow' Property='Visibility' Value='Visible'/></Trigger>
 <Trigger Property='IsChecked' Value='True'><Setter TargetName='Check' Property='Visibility' Value='Visible'/><Setter TargetName='Icon' Property='Visibility' Value='Collapsed'/></Trigger>
 <Trigger Property='IsHighlighted' Value='True'><Setter TargetName='Chrome' Property='Background' Value='@HOVER'/></Trigger>
 <Trigger Property='IsSubmenuOpen' Value='True'><Setter TargetName='Chrome' Property='Background' Value='@HOVER'/></Trigger>
 <Trigger Property='IsEnabled' Value='False'><Setter TargetName='Chrome' Property='Opacity' Value='.45'/></Trigger>
</ControlTemplate.Triggers>
</ControlTemplate>";
            item=new Style(typeof(MenuItem));item.Setters.Add(new Setter(Control.FocusVisualStyleProperty,null));item.Setters.Add(new Setter(Control.FontSizeProperty,13.0));
            item.Setters.Add(new Setter(Control.ForegroundProperty,Polish.Ink(light)));
            item.Setters.Add(new Setter(Control.BackgroundProperty,System.Windows.Media.Brushes.Transparent));
            item.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(8,7,8,7)));
            item.Setters.Add(new Setter(Control.TemplateProperty,(ControlTemplate)XamlReader.Parse(palette(itemXaml))));
            separator=new Style(typeof(Separator));
            separator.Setters.Add(new Setter(Control.TemplateProperty,(ControlTemplate)XamlReader.Parse(palette(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:glass='clr-namespace:Lumina;assembly=@ASSEMBLY' TargetType='Separator'><Border Height='1' Background='@LINE' Margin='28,5,10,5'/></ControlTemplate>"))));
            menu=new Style(typeof(ContextMenu));menu.Setters.Add(new Setter(FrameworkElement.MinWidthProperty,220.0));menu.Setters.Add(new Setter(ContextMenu.HasDropShadowProperty,false));
            menu.Setters.Add(new Setter(Control.ForegroundProperty,Polish.Ink(light)));
            menu.Setters.Add(new Setter(Control.BackgroundProperty,Polish.Color(background)));
            menu.Setters.Add(new Setter(Control.FontSizeProperty,13.0));
            menu.Setters.Add(new Setter(Control.TemplateProperty,(ControlTemplate)XamlReader.Parse(palette(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:glass='clr-namespace:Lumina;assembly=@ASSEMBLY' TargetType='ContextMenu'><Grid Margin='8'><Border BorderBrush='#60000000' BorderThickness='1' CornerRadius='14' IsHitTestVisible='False'><Border.Effect><DropShadowEffect BlurRadius='14' ShadowDepth='3' Opacity='.22'/></Border.Effect></Border><Border Background='{TemplateBinding Background}' glass:PopupChrome.Material='@MATERIAL' BorderBrush='@LINE' BorderThickness='1' CornerRadius='14' Padding='6' SnapsToDevicePixels='True'><ScrollViewer CanContentScroll='False' MaxHeight='420' VerticalScrollBarVisibility='Auto' HorizontalScrollBarVisibility='Disabled'><ItemsPresenter Grid.IsSharedSizeScope='True'/></ScrollViewer></Border></Grid></ControlTemplate>"))));
            item.Seal();separator.Seal();menu.Seal();
        }
        static MenuTheme For(bool light){MenuTheme value;if(!themes.TryGetValue(light,out value)){value=new MenuTheme(light);themes[light]=value;}return value;}
        public static void Install(Window window,bool light){var theme=For(light);window.Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)]=PopupChrome.ScrollStyle(light);window.Resources[typeof(ContextMenu)]=theme.menu;window.Resources[typeof(MenuItem)]=theme.item;window.Resources[typeof(Separator)]=theme.separator;}
        public static ContextMenu Create(bool light){var theme=For(light);var value=new ContextMenu{Style=theme.menu};value.Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)]=PopupChrome.ScrollStyle(light);value.Resources[typeof(MenuItem)]=theme.item;value.Resources[typeof(Separator)]=theme.separator;return value;}
    }
}


