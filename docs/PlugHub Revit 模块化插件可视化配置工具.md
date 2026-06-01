

# PlugHub Revit 模块化插件可视化配置工具

## 高级智能 UI/UX 设计方案规范文档 (工业级补强版)

### 1. 引言与设计愿景

`PlugHub` 作为面向 Revit 2020 的模块化插件框架，其动态发现、功能开关和组合排序功能赋予了界面极高的自由度。但在低版本 Revit（如 Revit 2020 及以下）中，API 存在硬性缺陷：**RibbonTab 或 RibbonPanel 一旦创建便无法在运行时动态销毁**。

本方案的核心目标在于打造一款 **“离线沙箱模拟” + “在线状态擦除”** 双引擎驱动的 WPF 可视化配置工具。方案将传统的“写配置文件”行为升级为类似 **Figma / H5 拖拽建站** 的高能交互体验，在深度适配 Revit 全谱系控件的同时，完美绕开低版本 API 限制，实现极简、高可视化的配置操作。

### 2. 核心架构：三栏式工作流布局

为了保证用户在编排复杂复合控件时的思维连贯性，界面采用经典的**三栏式布局**。该布局遵循“自左向右”的自然操作流：**检索组件 → 智能拖拽编排 → 原位/右侧属性微调**。

| **布局板块**                                      | **核心控件与实现**                                                      | **优化后的交互职能与细节方案**                                                                                                                 |
| --------------------------------------------- | ---------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------- |
| **左侧栏：可用组件库**<br>(Available Modules Pool)     | 带有实时过滤搜索框的 `ListBox`。数据源绑定至解包后的模块动态链接库 (DLL) 信息。                 | 支持按名称、作者、功能分类进行实时模糊检索。各条目配有清晰的图标，代表其原生 Revit 控件类型（如单按钮、下拉菜单、分裂按钮、单选组、文本框）。                                                        |
| **中间栏：WYSIWYG 核心画布**<br>(Revit Ribbon Canvas) | 高度自定义的 `TabControl` 与 `ItemsControl` 嵌套容器，**1:1 复刻 Revit 顶栏皮肤**。 | **水平流式排版：** 模拟真实 `Tab → Panel → Controls` 的横向嵌套空间感。**动态多态渲染：** 支持按钮堆叠、下拉菜单、分裂按钮等全谱系控件的原生视觉呈现。**状态视觉化：** 通过虚线框、半透明蒙版、状态徽标直观展示配置代价。 |
| **右侧栏：精细属性面板**<br>(Properties Panel)          | 垂直流式排列的 `ScrollViewer` 结合数据绑定的表单控件（`TextBox`, `ComboBox`）。       | 当用户在画布中选中特定元素时，动态激活该元素的属性编辑。作为“原位直改”的补充，承载诸如 Command ID、程序集路径、高级可见性权限等深层参数配置。                                                     |

### 3. 可视化布局增强：真·所见即所得画布

#### 3.1 像素级复刻 Revit 顶栏皮肤

- **视觉对齐：** 画布中的 Tab、Panel、Button 采用与 Revit 完全一致的配色（如浅灰 `#F2F2F2` 背景、暗灰选中态、以及特定的边框线条与字体）。

- **动态流式排版：** 利用 WPF 的自定义 `RibbonPanelLayout`，让画布中的控件尺寸、间距、换行规则与 Revit 保持 100% 一致。当用户拖入过多按钮时，画布上的 Panel 会像 Revit 一样自动拉宽，让用户在设计阶段就能直观发现“界面是否太拥挤”。

#### 3.2 低版本 API 限制下的状态视觉化

既然低版本 Revit 无法实时重建结构，但能实时控制显隐，工具在画布上通过**视觉特效**明确标示“执行代价”：

- **结构变更（需重启）：** 当用户改变控件类型或新增 Tab/Panel 结构时，该节点右上角显示灰色的 **“🔄 需重启” 悬浮微标**。

- **状态变更（立生效）：** 当用户勾选显隐（Visible）或启用（Enabled）状态时，画布上该控件**不消失**，而是**降低不透明度（Opacity = 0.4）并蒙上一层斜线遮罩**，同时显示绿色 **“⚡ 实时” 微标**。用户既能看到它在结构中的位置，又能一眼明白它当前在 Revit 中会被实时擦除。

#### 3.3 复合控件的“多级抽屉”可视化

对于 `PulldownButton`（下拉菜单）和 `SplitButton`（分裂按钮）等复合控件：

- **优化方案：** 控件右侧自带一个小型的下拉箭头。点击后，WPF 会弹出一个**悬浮的局部小画布（Popup 抽屉）**，直观展示点击下拉后弹出的子菜单。用户可以直接把左侧的命令拖拽进这个“悬浮抽屉”里，完成二级菜单的编排。

### 4. 全谱系 Revit Ribbon 控件矩阵适配

#### 4.1 复合控件数据模型 (ViewModel)

WPF 画布数据源不再是扁平列表，而是采用**分层树状复合结构**，完美映射以下 Revit 元素：

- **下拉菜单 (PulldownButton) 与 分裂按钮 (SplitButton)：** 作为容器节点，内部可无限追加 `PushButton`。SplitButton 额外支持 `DefaultCommand` 属性。

- **单选组合 (RadioGroup)：** 专属容器，内部只能放置 `ToggleButton`，且内部子项互斥（有且仅有一个被选中）。

- **文本框/组合框 (TextBox / ComboBox)：** 暴露出文本改变或选项改变的事件绑定接口。

- **面板折叠区 (Slide-out Panel)：** 在画布 Panel 尾部增加一条“折叠分割线”，拖到分割线下方的控件，在 Revit 中会自动归纳到点击面板小箭头才展开的隐藏区。

#### 4.2 XAML 多模板选择器 (DataTemplateSelector)

在中间的 WYSIWYG 画布中，利用 WPF 的 `DataTemplateSelector` 根据控件类型动态渲染：

XML

```
<DataTemplate x:Key="PulldownButtonTemplate">
    <StackPanel Orientation="Horizontal" Background="#F0F0F0" Margin="2" Padding="5">
        <Image Source="{Binding LargeImage}" Width="32" Height="32"/>
        <TextBlock Text="{Binding Text}" VerticalAlignment="Center" Margin="5,0"/>
        <Path Data="M 0 0 L 4 4 L 8 0 Z" Fill="#555555" VerticalAlignment="Center" Margin="2,0"/>
        <Popup IsOpen="{Binding IsDrawerOpen}" StaysOpen="False">
            <Border Background="#FFFFFF" BorderBrush="#CCCCCC" BorderThickness="1">
                <ItemsControl ItemsSource="{Binding ChildItems}" ItemTemplateSelector="{StaticResource RibbonControlTemplateSelector}"/>
            </Border>
        </Popup>
    </StackPanel>
</DataTemplate>

<DataTemplate x:Key="RadioGroupTemplate">
    <Border BorderBrush="#D0D0D0" BorderThickness="1" CornerRadius="2" Padding="5" Background="#FFFFFF">
        <StackPanel Orientation="Vertical">
            <TextBlock Text="[单选组合组]" FontSize="9" Foreground="DarkGray" Margin="0,0,0,4"/>
            <ItemsControl ItemsSource="{Binding ChildItems}">
                </ItemsControl>
        </StackPanel>
    </Border>
</DataTemplate>
```

### 5. 极简配置操作：傻瓜式高能交互

#### 5.1 智能落点预测与阻拦拖拽 (Foolproof Drag & Drop)

当用户从左侧库中拽起一个命令时：

- **合法落点高亮：** 画布中所有允许放入该控件的区域（例如 Panel 空白处、已有的 StackedGroup 槽位、Pulldown 的抽屉内部）立刻亮起**绿色的虚线框（Drop-Zone）**。

- **非法落点阻拦：** 如果一个 `StackedGroup` 已经垂直堆叠了 3 个按钮（Revit 的极限），当用户试图拖入第 4 个时，该区域变红，鼠标指针变为禁止符号，并在鼠标旁显示文字提示：“堆叠组已满，最多支持 3 个小图标”。

#### 5.2 原位直改 (In-place Editing) 与快捷气泡

- **原位双击重命名：** 允许用户在画布上直接双击按钮的文字标签，控件立刻变为 `TextBox` 编辑状态，敲回车直接完成重命名。

- **鼠标悬浮快捷气泡 (Context Flyout)：** 当鼠标悬停在画布的某个控件上时，其上方弹出一个极简的半透明气泡工具栏，包含最常用的高频操作：
  
  - `[👁 显隐开关]` 一键切换 Visible（低版本 Revit 实时生效）。
  
  - `[📐 切换大小]` 一键在大图标和小图标之间切换（触发重启微标）。
  
  - `[🗑 移出画布]` 一键删除。

#### 5.3 布局模板块机制 (Layout Presets)

在画布的 Panel 顶部提供一排常用布局模板按钮，如：**【标准大按钮】**、**【3键垂直堆叠组】**、**【常用分裂组】**。用户点击模板，画布上立刻生成对应的空白槽位外壳，用户只需要把左侧的命令扔进去“填空”即可，无需手动搭建复杂的嵌套关系。

#### 5.4 差异比对预览 (Diff Preview)

点击保存时，弹出一个直观的对比视窗，明确划分执行代价：

- `[删除]` 建筑工具箱 → 修改面板 → 复制按钮 （红色下划线）

- `[新增]` 结构工具箱 → 计算面板 → 梁柱解析 （绿色高亮，带 🔄 标记提示需重启生效）

- `[实时开关]` 通用工具箱 → 运维面板 → 日志导出 （黄色高亮，带 ⚡ 标记提示已实时擦除显隐）

> 当前第一阶段实现范围：先交付 `PushButton`、`PulldownButton`、`SplitButton`、`Stack` 的离线可视化配置器。`RadioGroup`、`TextBox`、`ComboBox`、`Slide-out Panel` 和在线 `LiveAgent` 作为后续阶段，需要单独扩展配置契约和 Revit 实机验证。

### 6. 技术落地实现要点

#### 6.1 拖拽引擎选型

放弃 WPF 原生生硬的拖拽事件，引入开源成熟的 **`GongSolutions.WPF.DragDrop`** 库。它天然支持 MVVM 架构，能够通过实现 `IDropTarget` 接口轻松做落点合法性校验，且能在拖拽时让鼠标下方跟着一个半透明的 Revit 真实按钮图标。

#### 6.2 基于 `AdornerLayer` 的视觉引导

利用 WPF 的**装饰层 (Adorner)** 来实现智能落点提示和报错信息流。这样既不会污染前端的 XAML 控件树结构，又能提供极佳的动画与半透明高亮反馈。

#### 6.3 核心数据结构双向同步机制

在 ViewModel 的设计中，让属性与配置实体及在线通信代理保持无缝联动：

C#

```
public class RibbonButtonViewModel : ViewModelBase
{
    private string _text;
    public string Text 
    {
        get => _text;
        set {
            if (SetProperty(ref _text, value)) {
                // 1. 同步给底层 PlugHub 配置实体
                UnderlyingConfig.Label = value; 
                // 2. 如果当前与 Revit 在线连接，实时擦除并更新 Revit 界面文案
                if (IsLiveConnected) {
                     PlugHubLiveAgent.UpdateItemText(UnderlyingConfig.Id, value);
                }
            }
        }
    }

    private bool _isVisible;
    public bool IsVisible 
    {
        get => _isVisible;
        set {
            if (SetProperty(ref _isVisible, value)) {
                UnderlyingConfig.IsVisible = value;
                // 3. 触发画布前端绑定，自动降低不透明度并蒙上斜线遮罩
                RaisePropertyChanged(nameof(CanvasOpacity)); 
                // 4. 低版本 Revit API 完全支持在线动态控制 Visible 属性
                if (IsLiveConnected) {
                     PlugHubLiveAgent.ToggleItemVisibility(UnderlyingConfig.Id, value);
                }
            }
        }
    }

    public double CanvasOpacity => IsVisible ? 1.0 : 0.4;
}
```

### 7. 进化版核心 XAML 布局源码参考

以下是支持**多态复合控件嵌套**、**水平流式复刻画布**的核心 XAML 层次布局：

XML

```
<Window x:Class="PlugHubConfigurator.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2000/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2000/xaml"
        xmlns:dd="clr-namespace:GongSolutions.Wpf.DragDrop;assembly=GongSolutions.Wpf.DragDrop"
        Title="PlugHub Revit 智能界面设计器" Height="750" Width="1300" Background="#F5F5F7">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="55"/> <RowDefinition Height="*"/>  <RowDefinition Height="30"/> </Grid.RowDefinitions>

        <Border Grid.Row="0" Background="#FFFFFF" BorderBrush="#E0E0E0" BorderThickness="0,0,0,1">
            <DockPanel Margin="15,0" LastChildFill="False">
                <TextBlock Text="PlugHub 界面设计器 (支持 Revit 2020 低版本动态擦除)" VerticalAlignment="Center" FontSize="15" FontWeight="Bold" Foreground="#1A3A5F"/>
                <StackPanel DockPanel.Dock="Right" Orientation="Horizontal" VerticalAlignment="Center">
                    <Button Content="布局模板" Style="{StaticResource SecondaryButtonStyle}" Margin="0,0,8,0"/>
                    <Button Content="保存并比对(Diff)" Style="{StaticResource PrimaryButtonStyle}"/>
                </StackPanel>
            </DockPanel>
        </Border>

        <Grid Grid.Row="1" Margin="10">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="280"/> <ColumnDefinition Width="*"/>   <ColumnDefinition Width="320"/> </Grid.ColumnDefinitions>

            <Border Grid.Column="0" Background="#FFFFFF" CornerRadius="6" BorderBrush="#E0E0E0" BorderThickness="1" Margin="5">
                <Grid Margin="12">
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="*"/>
                    </Grid.RowDefinitions>
                    <TextBox x:Name="SearchBox" Tag="检索可用插件命令..." Style="{StaticResource ModernSearchTextBoxStyle}" Margin="0,0,0,12"/>
                    <ListBox Grid.Row="1" ItemsSource="{Binding AvailableModules}"                              ItemTemplate="{StaticResource ModuleListItemTemplate}"
                             dd:DragDrop.IsDragSource="True" BorderThickness="0"/>
                </Grid>
            </Border>

            <Border Grid.Column="1" Background="#F2F2F2" CornerRadius="6" BorderBrush="#D0D0D0" BorderThickness="1" Margin="5">
                <Grid Margin="10">
                    <TabControl ItemsSource="{Binding RibbonTabs}" Style="{StaticResource RevitTabControlStyle}">
                        <TabControl.ContentTemplate>
                            <DataTemplate>
                                <ScrollViewer HorizontalScrollBarVisibility="Auto" VerticalScrollBarVisibility="Disabled">
                                    <ItemsControl ItemsSource="{Binding Panels}" dd:DragDrop.IsDropTarget="True" dd:DragDrop.DropHandler="{Binding}">
                                        <ItemsControl.ItemsPanel>
                                            <ItemsPanelTemplate>
                                                <StackPanel Orientation="Horizontal"/>
                                            </ItemsPanelTemplate>
                                        </ItemsControl.ItemsPanel>
                                        <ItemsControl.ItemTemplate>
                                            <DataTemplate>
                                                <Border BorderBrush="#D3D3D3" BorderThickness="1,1,1,2" Margin="4" Background="#F9F9F9" CornerRadius="2">
                                                    <Grid>
                                                        <Grid.RowDefinitions>
                                                            <RowDefinition Height="*"/>    <RowDefinition Height="Auto"/> <RowDefinition Height="22"/>   </Grid.RowDefinitions>

                                                        <TreeView ItemsSource="{Binding MainControls}"                                                                   ItemTemplateSelector="{StaticResource RibbonControlTemplateSelector}"
                                                                  dd:DragDrop.IsDragSource="True" dd:DragDrop.IsDropTarget="True"
                                                                  Style="{StaticResource HorizonTreeViewStyle}">
                                                            <TreeView.ItemsPanel>
                                                                <ItemsPanelTemplate>
                                                                    <StackPanel Orientation="Horizontal" VerticalAlignment="Center"/>
                                                                </ItemsPanelTemplate>
                                                            </TreeView.ItemsPanel>
                                                        </TreeView>

                                                        <ItemsControl Grid.Row="1" ItemsSource="{Binding SlideOutControls}" ... />

                                                        <TextBlock Grid.Row="2" Text="{Binding PanelName}" HorizontalAlignment="Center" FontSize="11" Foreground="#555555" VerticalAlignment="Center"/>
                                                    </Grid>
                                                </Border>
                                            </DataTemplate>
                                        </ItemsControl.ItemTemplate>
                                    </ItemsControl>
                                </ScrollViewer>
                            </DataTemplate>
                        </TabControl.ContentTemplate>
                    </TabControl>
                </Grid>
            </Border>

            <Border Grid.Column="2" Background="#FFFFFF" CornerRadius="6" BorderBrush="#E0E0E0" BorderThickness="1" Margin="5">
                <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="15">
                    <StackPanel DataContext="{Binding SelectedElement}">
                        <TextBlock Text="属性微调 (支持原位双击直改)" FontSize="13" FontWeight="Bold" Foreground="#2980b9" Margin="0,0,0,15"/>
                        <TextBlock Text="显示标签 (Label)" Margin="0,4"/>
                        <TextBox Text="{Binding Text, UpdateSourceTrigger=PropertyChanged}" Margin="0,0,0,12"/>
                        <TextBlock Text="唯一命令 ID (Command ID)" Margin="0,4"/>
                        <TextBox Text="{Binding CommandId}" IsReadOnly="True" Background="#F5F5F5" Margin="0,0,0,12"/>
                        <TextBlock Text="提示信息 (Tooltip)" Margin="0,4"/>
                        <TextBox Text="{Binding Tooltip}" Height="55" TextWrapping="Wrap" AcceptsReturn="True" Margin="0,0,0,12"/>
                        <CheckBox Content="默认在 Ribbon 中可见 (实时同步)" IsChecked="{Binding IsVisible}" Margin="0,5"/>
                    </StackPanel>
                </ScrollViewer>
            </Border>
        </Grid>

        <StatusBar Grid.Row="2" Background="#F5F5F7" Foreground="#7F8C8D" FontSize="11" Padding="10,0">
            <StatusBarItem Content="环境连接: Revit 2020 (已建立本地动态代理管道)"/>
            <Separator/>
            <StatusBarItem Content="智能校验: 2 个节点处于 [🔄 需重启] 状态，显隐开关将 [⚡ 实时生效]"/>
        </StatusBar>
    </Grid>
</Window>
```
