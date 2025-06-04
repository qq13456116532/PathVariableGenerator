using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices; // 需要这个来进行 P/Invoke
using System.Windows.Forms;         // 需要这个来显示 MessageBox
using System.IO;                    // 需要这个来进行路径操作

namespace AddToSystemPathUtil
{
    class Program
    {
        // 定义 SendMessageTimeout 函数，用于广播环境变量更改
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessageTimeout(
            IntPtr hWnd,
            uint Msg,
            UIntPtr wParam,
            string lParam,
            uint fuFlags,
            uint uTimeout,
            out UIntPtr lpdwResult);

        // Windows 消息常量
        private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);
        private const uint WM_SETTINGCHANGE = 0x1A;
        private const uint SMTO_ABORTIFHUNG = 0x0002; // 如果接收方挂起，则不等待

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                MessageBox.Show("错误：未提供文件夹路径。", "添加到系统Path", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.ExitCode = 1; // 设置退出代码
                return;
            }

            string folderPath = args[0];

            try
            {
                // 标准化路径并检查是否存在
                folderPath = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!Directory.Exists(folderPath))
                {
                    MessageBox.Show($"错误：文件夹 '{folderPath}' 不存在。", "添加到系统Path", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Environment.ExitCode = 2;
                    return;
                }

                // 获取当前的系统 PATH 环境变量
                string currentSystemPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine);
                List<string> pathEntries = new List<string>();

                if (!string.IsNullOrEmpty(currentSystemPath))
                {
                    // 按分号分割，移除空条目和首尾空格
                    pathEntries.AddRange(currentSystemPath.Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries)
                                                         .Select(p => p.Trim())
                                                         .Where(p => !string.IsNullOrEmpty(p)));
                }

                // 检查路径是否已存在 (不区分大小写比较)
                // 同时处理末尾是否有斜杠的情况
                if (pathEntries.Any(p => string.Equals(p.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                                                        folderPath,
                                                        StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show($"信息：文件夹 '{folderPath}' 已经存在于系统 PATH 中。", "添加到系统Path", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Environment.ExitCode = 0; // 成功，但未做更改
                    return;
                }

                // 将新路径添加到列表
                pathEntries.Add(folderPath);
                string newSystemPath = string.Join(Path.PathSeparator.ToString(), pathEntries);

                // 设置新的系统 PATH 环境变量
                Environment.SetEnvironmentVariable("Path", newSystemPath, EnvironmentVariableTarget.Machine);

                // 广播环境变量已更改的消息
                UIntPtr result; // 用于接收 SendMessageTimeout 的结果
                SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, UIntPtr.Zero, "Environment", SMTO_ABORTIFHUNG, 1000, out result);

                MessageBox.Show($"成功：文件夹 '{folderPath}' 已添加到系统 PATH。`n`n请注意：您可能需要重新启动活动的应用程序或打开新的命令提示符才能使更改完全生效。", "添加到系统Path", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Environment.ExitCode = 0;
            }
            catch (System.Security.SecurityException secEx)
            {
                 MessageBox.Show($"安全错误：此操作需要管理员权限才能修改系统环境变量。`n详细信息: {secEx.Message}", "添加到系统Path - 权限错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                 Environment.ExitCode = 3;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发生错误：{ex.Message}", "添加到系统Path - 错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.ExitCode = 4;
            }
        }
    }
}