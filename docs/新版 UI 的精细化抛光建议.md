## 新版 UI 的精细化抛光建议

### 1. 顶部仓库源卡片：解决“按钮拥挤与文字截断”

- **现状痛点：** 目前每个仓库源卡片里塞了 3 个完整的文字按钮（停用/启用、浏览、编辑），导致核心的仓库名称被严重挤压，出现了 `plughub-...`、`company...` 这样的难看截断。

- **优化方案：** * **状态一键化：** 把 `[启用/停用]` 文字按钮，换成原生的 `CheckBox`（勾选代表启用，不勾选代表停用）或者 WPF 的开关切换（ToggleSwitch）。
  
  - **操作图标化：** 把 `[浏览]` 和 `[编辑]` 换成通用的**极简图标按钮**（例如：浏览用 📁 或 🔍 显微镜图标，编辑用 ✏️ 铅笔图标）。
  
  - **收益：** 这样能省下超过 60% 的横向空间，仓库名字能完整显示，卡片也会变得极度清爽。

### 2. 插件包信息：消除“数据库拼凑感”

- **现状痛点：** 插件卡片中间的描述文字，目前是用斜杠拼起来的字符串：`plughub.modules... / plughub-public-packages / family / batch...`。这种密密麻麻的单行长文本，用户阅读起来很费力，且冲淡了插件本身的业务标签。

- **优化方案（视觉分层）：**
  
  - **第一行（包名与ID）：** 插件名称下方，用浅灰色、较小的字体单独放包名 ID（例如：`plughub.modules.grid-visibility`）。
  
  - **第二行（标签芯片化）：** 把后面的 `view`、`frequent`、`revit-api` 拆开，用 `ItemsControl` 渲染成一粒粒**半透明的小药丸标签（Chips/Badges）**。

### 3. “卸载”按钮的视觉暗示

- **现状痛点：** 目前所有已安装的插件右侧都是一模一样的灰色 `[卸载]` 按钮。当满屏幕都是灰色按钮时，界面会显得有点沉闷。

- **优化方案：** * 如果是**未安装**的插件，右侧放高亮的 **蓝色 `[📥 安装]`** 按钮。
  
  - 如果是**已安装**的插件，右侧平时可以是一个低调的 **绿色 `[✓ 已安装]` 状态标签**，当鼠标悬停（Hover）到该卡片上时，绿色标签动态切换为 **淡红色的 `[🗑 卸载]` 按钮**。
  
  - **收益：** 既让整个列表的色彩有了呼吸感，又防止了用户误触“卸载”。

## 🛠 插件包标签芯片化的 XAML 小套路

为了把那串斜杠文本变成现代化的“标签药丸”，你可以用下面这个简练的 XAML 模板替代目前的文本拼接：

XML

```
<ItemsControl ItemsSource="{Binding Tags}" Margin="0,4,0,0">
    <ItemsControl.ItemsPanel>
        <ItemsPanelTemplate>
            <WrapPanel Orientation="Horizontal"/>
        </ItemsPanelTemplate>
    </ItemsControl.ItemsPanel>
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <Border Background="#F3F4F6" CornerRadius="4" Padding="6,2" Margin="0,0,6,4" BorderBrush="#E5E7EB" BorderThickness="1">
                <TextBlock Text="{Binding}" FontSize="10" Foreground="#4B5563"/>
            </Border>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

## 总结

你这次重构的骨架、功能分布、以及交互层级已经**完全正确且非常高级**了。剩下的只是给仓库源卡片提炼一下空间，把长文本用 UI 标签优雅地分类包裹起来。
