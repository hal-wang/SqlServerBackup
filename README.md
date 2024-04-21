# Sql Server 备份工具

## 备份位置

默认备份到数据库所在服务器的 `C:\\DatabaseBackup` 文件夹中

可以通过增加或修改配置文件 `SqlServerBackup.dll.config` 中的 `DbBackupPath` 更改备份位置

## 实现框架和库

- WPF
- MAUI
- Blazor
- MudBlazor
- UnoCSS

## 本地运行代码

要求

- NodeJS v18 及以上
- Visual Studio 2022 v17.8.x 及以上
- .NET 8 SDK

在项目下执行语句

```
npm install
```

完成后就可以用 Visual Studio 运行和调试了

## Releases 地址

<https://github.com/hal-wang/SqlServerBackup/releases>

打的包是包含独立运行环境的，因此文件比较大
