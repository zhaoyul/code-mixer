#!/bin/bash

# 用于混淆 PLCConnector 项目的脚本
# 请在执行前确保您已备份项目

echo "开始混淆 PLCConnector 项目..."
echo "警告：这将修改您的源代码文件，请确保已备份！"
echo

# 检查是否在正确的目录下
if [ ! -f "/Users/a123/sandbox/rc/chutian/src/TrukingSys/PLCConnector/PLCConnector.csproj" ]; then
    echo "错误：未找到 PLCConnector 项目文件"
    exit 1
fi

echo "找到 PLCConnector 项目文件，开始执行混淆..."

# 检查是否已编译 SharpDeceiver
if [ ! -f "./SharpDeceiver/bin/Release/net8.0/SharpDeceiver.dll" ]; then
    echo "错误：未找到编译后的 SharpDeceiver 工具"
    exit 1
fi

# 切换到 TrukingSys 解决方案目录以确保正确解析依赖
cd /Users/a123/sandbox/rc/chutian/src/TrukingSys

echo "正在执行混淆操作..."
dotnet /Users/a123/sandbox/rc/code-mixer/SharpDeceiver/bin/Release/net8.0/SharpDeceiver.dll \
  --mode obfuscate \
  --path PLCConnector/PLCConnector.csproj \
  --map PLCConnector/plc_deceiver_map.json

if [ $? -eq 0 ]; then
    echo
    echo "混淆操作完成！"
    echo "映射文件已保存至：/Users/a123/sandbox/rc/chutian/src/TrukingSys/PLCConnector/plc_deceiver_map.json"
    echo "请保留此映射文件，以便将来还原代码。"
    echo
    echo "注意：请测试混淆后的代码以确保其正常工作。"
else
    echo
    echo "混淆操作失败，请检查错误信息。"
fi
