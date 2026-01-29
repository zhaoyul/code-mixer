# 混淆 /Users/a123/sandbox/rc/chutian/src/TrukingSys/PLCConnector 项目的说明

## 重要提醒
在执行混淆前，请确保：
1. 已提交所有代码到版本控制系统（如 Git）
2. 或者已备份整个项目目录
3. 混淆操作会直接修改源代码文件，只有通过映射文件才能还原

## 步骤 1：备份项目
在执行混淆前，请备份项目：
```bash
cp -r /Users/a123/sandbox/rc/chutian/src/TrukingSys/PLCConnector /Users/a123/sandbox/rc/chutian/src/TrukingSys/PLCConnector_backup
```

## 步骤 2：执行混淆
运行以下命令来混淆 PLCConnector 项目：
```bash
./run_obfuscation.sh
```

## 步骤 3：验证混淆结果
混淆完成后，检查项目是否仍能正常编译和运行：
```bash
cd /Users/a123/sandbox/rc/chutian/src/TrukingSys
dotnet build PLCConnector/PLCConnector.csproj
```

## 如需还原代码
如果需要将代码还原到原始状态，请运行：
```bash
./run_restore.sh
```

## 注意事项
- 混淆后的代码将保留功能但标识符名称将被替换
- 请保留生成的 `plc_deceiver_map.json` 文件，这是还原代码的唯一途径
- 某些使用反射的代码可能在混淆后失效
- 混淆后请务必测试应用程序以确保功能正常

## 脚本说明
- `run_obfuscation.sh`: 执行混淆操作
- `run_restore.sh`: 执行还原操作