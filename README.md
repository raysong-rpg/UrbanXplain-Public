# UrbanXplain

[English](#english) | [中文](#中文)

---

## English

**UrbanXplain** - A language-driven urban planning system combining real-time 3D simulation with AI-powered explainable reasoning.

### 🌟 Project Overview

UrbanXplain is an intelligent urban planning tool developed with Unity. Users can generate and visualize city planning proposals through natural language interaction. The system uses AI to create optimized building layouts and provides transparent reasoning for each design decision.

### ✨ Core Features

#### 🤖 AI-Powered Planning
- **Natural Language Interface**: Describe your urban planning needs in simple language
- **Intelligent Layout Generation**: AI automatically allocates 43 plots based on requirements
- **Explainable Decisions**: Each building placement comes with detailed reasoning
- **Real-time Streaming Generation**: Watch the city progressively generated during AI processing

#### 🏙️ 3D Visualization
- **Interactive 3D Environment**: Navigate and explore generated urban plans
- **Building Information System**: Click any building to view detailed information
- **Visual Feedback**: Color-coded buildings by function and energy efficiency
- **Dynamic Generation**: Buildings appear in real-time during AI processing

#### 🏗️ Building Types & Constraints
- **Residential** (1-30 floors, no super high-rise allowed)
- **Commercial** (4-30+ floors, no mid-rise allowed)
- **Public Services** (9-30 floors, no low-rise allowed)
- **Cultural & Entertainment** (large plots must use high-rise glass curtain walls)

#### ⚡ Energy Efficiency
- Energy consumption scoring (1-100)
- Material-based optimization (glass curtain wall vs. concrete)
- Visualized energy efficiency metrics
- Sustainability considerations in planning

### 🛠️ Tech Stack

- **Engine**: Unity (C#)
- **AI Integration**: DeepSeek API (deepseek-chat model)
- **Data Format**: JSON-based building specifications
- **Architecture**: Modular component design

### 📁 Project Structure

```
UrbanXplain/
├── Assets/
│   ├── UrbanXplain/
│   │   ├── Scripts/
│   │   │   ├── Urban/           # Core planning logic
│   │   │   ├── UI/              # User interface
│   │   │   ├── Tools/           # Utility scripts
│   │   │   └── Editor/          # Unity editor extensions
│   │   ├── Presets/             # Pre-configured city plans
│   │   └── UrbanXplain.unity    # Main scene
│   └── StreamingAssets/         # Runtime data (CSV files)
├── ProjectSettings/
└── config.example.json          # API configuration template
```

### 🚀 Quick Start

#### System Requirements
- Unity 2022.3 or higher
- Windows 10/11 (primary development platform)
- DeepSeek API key (apply separately)

#### Installation Steps

1. **Clone the repository**:
   ```bash
   git clone https://github.com/raysong-rpg/UrbanXplain-Public.git
   ```

2. **Configure DeepSeek API Key**:

   The project uses DeepSeek API for AI-driven urban planning. You need to:

   a. Visit [DeepSeek Platform](https://platform.deepseek.com/) to register and obtain an API Key

   b. Create configuration file in the project root directory:
   ```bash
   # Copy configuration template
   cp config.example.json config.json
   ```

   c. Edit `config.json` and fill in your API Key:
   ```json
   {
     "deepseek_api_key": "your-api-key-here",
     "deepseek_api_url": "https://api.deepseek.com/v1/chat/completions"
   }
   ```

   ⚠️ **Important**: `config.json` has been added to `.gitignore` and will not be committed to the Git repository. Please keep your API Key secure.

3. **Open the project in Unity**

4. **Load the main scene**: `Assets/UrbanXplain/UrbanXplain.unity`

5. **Click Play to start the application**

#### Usage

1. **Launch the application** - Run in Unity Editor
2. **Input planning requirements** - Describe in the input box (e.g., "Create a modern eco-friendly community")
3. **Submit for generation** - Click submit to generate city plan
4. **Explore the 3D city** - Browse the generated city
5. **View building information** - Click on buildings to view details

### 📊 Data Files

- **buildingprefab.csv**: Building prefab configurations
- **emptyland.csv**: Size and location of 43 empty plots
- **Preset JSONs**: Pre-configured city planning schemes

### 🎮 Controls

#### Navigation
- **P**: Toggle Controls Menu
- **Alt**: Toggle between Input Mode / Gameplay Mode

#### Move
- **WASD**: Move (Forward, Backward, Left, Right)
- **E/Q**: Move Up/Down
- **Shift**: Speed up movement

#### Information
- **1**: Highlight functional zoning
- **2**: Show energy performance
- **LMB**: View Design Rationale (Left Mouse Button)

### 🤝 Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

### 📝 License

This project is licensed under the terms specified in the LICENSE file.

### 🌐 Links

- **Repository**: [https://github.com/raysong-rpg/UrbanXplain-Public](https://github.com/raysong-rpg/UrbanXplain-Public)
- **Issue Tracker**: [Report bugs or request features](https://github.com/raysong-rpg/UrbanXplain-Public/issues)

### 👥 Author

- **raysong-rpg** - Primary Developer

### 🙏 Acknowledgments

- Fantastic City Generator - Third-party city generation assets
- DeepSeek - AI model provider
- Unity Technologies - Game engine

---

## 中文

**UrbanXplain** - 语言驱动的城市规划系统，结合实时3D仿真与AI驱动的可解释推理。

### 🌟 项目概述

UrbanXplain 是一个基于Unity开发的智能城市规划工具，用户可以通过自然语言交互来生成和可视化城市规划方案。系统利用AI创建优化的建筑布局，并为每个设计决策提供透明的推理说明。

### ✨ 核心功能

#### 🤖 AI智能规划
- **自然语言界面**：用简单的语言描述您的城市规划需求
- **智能布局生成**：AI自动根据需求分配43个地块
- **可解释决策**：每个建筑放置都附带详细的理由说明
- **实时流式生成**：观察城市在AI处理过程中逐步生成

#### 🏙️ 3D可视化
- **交互式3D环境**：导航和探索生成的城市规划
- **建筑信息系统**：点击任何建筑查看详细信息
- **视觉反馈**：按功能和能源效率对建筑进行颜色编码
- **动态生成**：AI处理时建筑实时出现

#### 🏗️ 建筑类型与约束
- **住宅** (1-30层，不允许超高层)
- **商业** (4-30+层，不允许中层)
- **公共服务** (9-30层，不允许低层)
- **文化娱乐** (大地块必须使用高层玻璃幕墙)

#### ⚡ 能源效率
- 能源消耗评分 (1-100)
- 基于材料的优化（玻璃幕墙 vs 混凝土）
- 可视化能源效率指标
- 规划中的可持续性考量

### 🛠️ 技术栈

- **引擎**：Unity (C#)
- **AI集成**：DeepSeek API (deepseek-chat模型)
- **数据格式**：基于JSON的建筑规格
- **架构**：模块化组件设计

### 📁 项目结构

```
UrbanXplain/
├── Assets/
│   ├── UrbanXplain/
│   │   ├── Scripts/
│   │   │   ├── Urban/           # 核心规划逻辑
│   │   │   ├── UI/              # 用户界面
│   │   │   ├── Tools/           # 工具脚本
│   │   │   └── Editor/          # Unity编辑器扩展
│   │   ├── Presets/             # 预配置城市规划
│   │   └── UrbanXplain.unity    # 主场景
│   └── StreamingAssets/         # 运行时数据（CSV文件）
├── ProjectSettings/
└── config.example.json          # API配置模板
```

### 🚀 快速开始

#### 系统要求
- Unity 2022.3 或更高版本
- Windows 10/11（主要开发平台）
- DeepSeek API密钥（需自行申请）

#### 安装步骤

1. **克隆仓库**：
   ```bash
   git clone https://github.com/raysong-rpg/UrbanXplain-Public.git
   ```

2. **配置 DeepSeek API Key**：

   项目使用 DeepSeek API 进行 AI 驱动的城市规划。你需要：

   a. 访问 [DeepSeek 官网](https://platform.deepseek.com/) 注册并获取 API Key

   b. 在项目根目录创建配置文件：
   ```bash
   # 复制配置模板
   cp config.example.json config.json
   ```

   c. 编辑 `config.json`，填入你的 API Key：
   ```json
   {
     "deepseek_api_key": "你的API密钥",
     "deepseek_api_url": "https://api.deepseek.com/v1/chat/completions"
   }
   ```

   ⚠️ **重要**：`config.json` 已被添加到 `.gitignore`，不会被提交到 Git 仓库，请妥善保管你的 API Key。

3. **在Unity中打开项目**

4. **加载主场景**：`Assets/UrbanXplain/UrbanXplain.unity`

5. **点击播放启动应用**

#### 使用方法
1. **启动应用** - 在Unity编辑器中运行
2. **输入规划需求** - 在输入框中描述（如："创建一个现代环保社区"）
3. **提交生成** - 点击提交生成城市规划
4. **探索3D城市** - 浏览生成的城市
5. **查看建筑信息** - 点击建筑查看详细信息

### 📊 数据文件

- **buildingprefab.csv**：建筑预制体配置
- **emptyland.csv**：43个空地块的尺寸和位置
- **预设JSON**：预配置的城市规划方案

### 🎮 操作控制

#### 导航
- **P**：切换控制菜单
- **Alt**：切换输入模式/游玩模式

#### 移动
- **WASD**：移动（前、后、左、右）
- **E/Q**：上升/下降
- **Shift**：加速移动

#### 信息查看
- **1**：高亮功能分区
- **2**：显示能源效率
- **鼠标左键**：查看设计理由

### 🤝 贡献指南

欢迎贡献！请随时提交问题和拉取请求。

### 📝 许可证

本项目根据LICENSE文件中指定的条款进行许可。

### 🌐 相关链接

- **仓库地址**：[https://github.com/raysong-rpg/UrbanXplain-Public](https://github.com/raysong-rpg/UrbanXplain-Public)
- **问题反馈**：[报告bug或请求功能](https://github.com/raysong-rpg/UrbanXplain-Public/issues)

### 👥 作者

- **raysong-rpg** - 主要开发者

### 🙏 致谢

- Fantastic City Generator - 第三方城市生成资源
- DeepSeek - AI模型提供商
- Unity Technologies - 游戏引擎
