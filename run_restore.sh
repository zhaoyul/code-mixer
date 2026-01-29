#!/bin/bash

# 用于还原 PLCConnector 项目混淆的脚本
# 请在执行前确保您有映射文件

echo "开始还原 PLCConnector 项目..."
echo "警告：这将修改您的源代码文件，请确保有备份！"
echo

# 检查映射文件是否存在
if [ ! -f "/Users/a123/sandbox/rc/chutian/src/TrukingSys/PLCConnector/plc_deceiver_map.json" ]; then
    echo "错误：未找到映射文件 plc_deceiver_map.json"
    echo "请确保您在混淆时保留了映射文件"
    exit 1
fi

# 检查是否已编译 SharpDeceiver
if [ ! -f "/Users/a123/sandbox/rc/code-mixer/SharpDeceiver/bin/Release/net8.0/SharpDeceiver.dll" ]; then
    echo "错误：未找到编译后的 SharpDeceiver 工具"
    exit 1
fi

# 切换到 TrukingSys 解决方案目录
cd /Users/a123/sandbox/rc/chutian/src/TrukingSys

echo "正在执行还原操作..."
dotnet /Users/a123/sandbox/rc/code-mixer/SharpDeceiver/bin/Release/net8.0/SharpDeceiver.dll \
  --mode restore \
  --path PLCConnector/PLCConnector.csproj \
  --map PLCConnector/plc_deceiver_map.json

if [ $? -eq 0 ]; then
    echo
    echo "还原操作完成！"
    echo "PLCConnector 项目的代码已成功还原。"
else
    echo
    echo "还原操作失败，请检查错误信息。"
fi