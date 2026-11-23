using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.UI;

namespace ContaMobi.Helpers
{
    public static class UIThemeHelper
    {
        private const int GWLP_HWNDPARENT = -8;
        private const int GWLP_WNDPROC = -4;
        private const uint WM_DESTROY = 0x0002;
        private const uint WM_CLOSE = 0x0010;
        private const uint WM_NCDESTROY = 0x0082;

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        private delegate bool EnumThreadWndProc(IntPtr hWnd, IntPtr lParam);

        private static readonly WndProcDelegate _customWndProc = SubclassWndProc;
        private static readonly Dictionary<IntPtr, IntPtr> _origWndProcs = new();

        private static IntPtr _mainWindowHwnd = IntPtr.Zero;
        private static readonly Stack<IntPtr> _pilhaModais = new();
        private static readonly Dictionary<IntPtr, IntPtr> _mapaFilhoParaPai = new();

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr SetActiveWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnableWindow(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool bEnable);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumThreadWindows(uint dwThreadId, EnumThreadWndProc lpfn, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private static void DefinirOwnerWin32(IntPtr hWndFilho, IntPtr hWndPai)
        {
            if (hWndFilho == IntPtr.Zero || !IsWindow(hWndFilho)) return;

            if (IntPtr.Size == 8)
                SetWindowLongPtr64(hWndFilho, GWLP_HWNDPARENT, hWndPai);
            else
                SetWindowLong32(hWndFilho, GWLP_HWNDPARENT, hWndPai.ToInt32());
        }

        private static void InstalarInterceptadorFechamento(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || _origWndProcs.ContainsKey(hWnd)) return;

            IntPtr pfnNovo = Marshal.GetFunctionPointerForDelegate(_customWndProc);
            IntPtr pfnAntigo = IntPtr.Size == 8
                ? SetWindowLongPtr64(hWnd, GWLP_WNDPROC, pfnNovo)
                : new IntPtr(SetWindowLong32(hWnd, GWLP_WNDPROC, pfnNovo.ToInt32()));

            if (pfnAntigo != IntPtr.Zero)
            {
                _origWndProcs[hWnd] = pfnAntigo;
            }
        }

        private static IntPtr SubclassWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_CLOSE || msg == WM_DESTROY)
            {
                // Reabilita a janela pai ANTES que o Windows destrua a janela modal
                LiberarModal(hWnd);
            }

            if (_origWndProcs.TryGetValue(hWnd, out IntPtr pfnAntigo) && pfnAntigo != IntPtr.Zero)
            {
                if (msg == WM_NCDESTROY)
                {
                    _origWndProcs.Remove(hWnd);
                }
                return CallWindowProc(pfnAntigo, hWnd, msg, wParam, lParam);
            }

            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        /// <summary>
        /// Aplica o tema retrô na barra de título e ativa automaticamente a navegação com ENTER (como TAB) na janela.
        /// </summary>
        public static void AplicarBarraTituloRetro(Window janela)
        {
            if (janela == null) return;

            EnterNavegacaoHelper.HabilitarEnterComoTab(janela);
            AplicarBarraTituloRetro(janela.AppWindow);
        }

        public static void AplicarBarraTituloRetro(AppWindow appWindow)
        {
            if (appWindow == null) return;

            string caminhoIcone = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (System.IO.File.Exists(caminhoIcone))
            {
                appWindow.SetIcon(caminhoIcone);
            }

            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var titleBar = appWindow.TitleBar;
                titleBar.BackgroundColor = Color.FromArgb(255, 0, 0, 0);
                titleBar.ForegroundColor = Color.FromArgb(255, 0, 255, 102);
                titleBar.InactiveBackgroundColor = Color.FromArgb(255, 5, 16, 5);
                titleBar.InactiveForegroundColor = Color.FromArgb(255, 0, 120, 50);

                titleBar.ButtonBackgroundColor = Color.FromArgb(255, 0, 0, 0);
                titleBar.ButtonForegroundColor = Color.FromArgb(255, 0, 255, 102);
                titleBar.ButtonHoverBackgroundColor = Color.FromArgb(255, 0, 68, 25);
                titleBar.ButtonHoverForegroundColor = Color.FromArgb(255, 255, 255, 255);
                titleBar.ButtonPressedBackgroundColor = Color.FromArgb(255, 0, 255, 102);
                titleBar.ButtonPressedForegroundColor = Color.FromArgb(255, 0, 0, 0);
                titleBar.ButtonInactiveBackgroundColor = Color.FromArgb(255, 5, 16, 5);
                titleBar.ButtonInactiveForegroundColor = Color.FromArgb(255, 0, 120, 50);
            }

            IntPtr hWndAtual = Win32Interop.GetWindowFromWindowId(appWindow.Id);

            bool ehJanelaRaiz = VerificarSeChamadorEhJanelaRaiz(out bool ehMainWindow);
            if (ehMainWindow && hWndAtual != IntPtr.Zero)
            {
                _mainWindowHwnd = hWndAtual;
            }

            if (!ehJanelaRaiz && hWndAtual != IntPtr.Zero)
            {
                VincularModalPorHwnd(appWindow, hWndAtual, IntPtr.Zero);
            }
        }

        public static void AbrirModal(Window janelaFilha, Window? janelaPai = null)
        {
            if (janelaFilha == null) return;

            EnterNavegacaoHelper.HabilitarEnterComoTab(janelaFilha);

            IntPtr hWndFilha = WinRT.Interop.WindowNative.GetWindowHandle(janelaFilha);
            IntPtr hWndPai = janelaPai != null
                ? WinRT.Interop.WindowNative.GetWindowHandle(janelaPai)
                : IntPtr.Zero;

            VincularModalPorHwnd(janelaFilha.AppWindow, hWndFilha, hWndPai);

            janelaFilha.Closed += (s, e) => LiberarModal(hWndFilha);
            janelaFilha.Activate();
        }

        public static Window AbrirPaginaModal<TPage>(string titulo, int largura = 1180, int altura = 760) where TPage : Page, new()
        {
            var hostWindow = new Window
            {
                Title = titulo,
                Content = new TPage()
            };

            AplicarBarraTituloRetro(hostWindow);
            hostWindow.AppWindow.Resize(new SizeInt32(largura, altura));
            AbrirModal(hostWindow);
            return hostWindow;
        }

        private static void VincularModalPorHwnd(AppWindow appWindow, IntPtr hWndFilho, IntPtr hWndPaiExplicito)
        {
            if (hWndFilho == IntPtr.Zero) return;

            LimparPilhaJanelasInvalidas();

            IntPtr hWndPai = hWndPaiExplicito;

            if (hWndPai == IntPtr.Zero || hWndPai == hWndFilho)
            {
                if (_pilhaModais.Count > 0 && _pilhaModais.Peek() != hWndFilho)
                {
                    hWndPai = _pilhaModais.Peek();
                }
                else
                {
                    IntPtr ativo = GetActiveWindow();
                    if (ativo != IntPtr.Zero && ativo != hWndFilho && IsWindow(ativo))
                        hWndPai = ativo;
                    else
                        hWndPai = _mainWindowHwnd;
                }
            }

            if (hWndPai == IntPtr.Zero || hWndPai == hWndFilho || !IsWindow(hWndPai))
                return;

            InstalarInterceptadorFechamento(hWndFilho);

            if (_mapaFilhoParaPai.ContainsKey(hWndFilho))
            {
                if (hWndPaiExplicito != IntPtr.Zero && hWndPaiExplicito != _mapaFilhoParaPai[hWndFilho])
                {
                    EnableWindow(_mapaFilhoParaPai[hWndFilho], true);
                    _mapaFilhoParaPai[hWndFilho] = hWndPaiExplicito;
                    DefinirOwnerWin32(hWndFilho, hWndPaiExplicito);
                    EnableWindow(hWndPaiExplicito, false);
                }
                EnableWindow(hWndFilho, true);
                return;
            }

            _mapaFilhoParaPai[hWndFilho] = hWndPai;
            _pilhaModais.Push(hWndFilho);

            DefinirOwnerWin32(hWndFilho, hWndPai);
            EnableWindow(hWndPai, false);
            EnableWindow(hWndFilho, true);

            appWindow.Closing += (s, e) => LiberarModal(hWndFilho);
            appWindow.Destroying += (s, e) => LiberarModal(hWndFilho);

            var dq = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            if (dq != null)
            {
                dq.TryEnqueue(() =>
                {
                    try
                    {
                        if (appWindow.Presenter is OverlappedPresenter presenter)
                        {
                            presenter.IsMaximizable = false;
                            presenter.IsMinimizable = false;
                        }

                        CentralizarSobreJanelaPai(appWindow, hWndPai);
                        EnableWindow(hWndFilho, true);
                        SetForegroundWindow(hWndFilho);
                        SetActiveWindow(hWndFilho);
                    }
                    catch { }
                });
            }
        }

        private static void LiberarModal(IntPtr hWndFilho)
        {
            if (hWndFilho == IntPtr.Zero) return;

            IntPtr hWndPaiParaAtivar = IntPtr.Zero;

            if (_mapaFilhoParaPai.TryGetValue(hWndFilho, out IntPtr hWndPai))
            {
                _mapaFilhoParaPai.Remove(hWndFilho);
                hWndPaiParaAtivar = hWndPai;

                // Desfaz o vínculo de Owner antes que o DestroyWindow finalize
                DefinirOwnerWin32(hWndFilho, IntPtr.Zero);
            }

            RemoverDaPilha(hWndFilho);
            LimparPilhaJanelasInvalidas();

            if (hWndPaiParaAtivar != IntPtr.Zero && IsWindow(hWndPaiParaAtivar))
            {
                EnableWindow(hWndPaiParaAtivar, true);
                SetForegroundWindow(hWndPaiParaAtivar);
                SetActiveWindow(hWndPaiParaAtivar);
            }
            else if (_pilhaModais.Count > 0)
            {
                IntPtr topo = _pilhaModais.Peek();
                if (topo != IntPtr.Zero && IsWindow(topo))
                {
                    EnableWindow(topo, true);
                    SetForegroundWindow(topo);
                    SetActiveWindow(topo);
                }
            }
            else
            {
                // Se não restou nenhum modal na pilha, reabilita todas as janelas da thread principal
                ReabilitarTodasJanelasDaThread(hWndFilho);
            }
        }

        private static void RemoverDaPilha(IntPtr hWndAlvo)
        {
            if (_pilhaModais.Count == 0) return;

            var temp = new Stack<IntPtr>();
            while (_pilhaModais.Count > 0)
            {
                IntPtr topo = _pilhaModais.Pop();
                if (topo != hWndAlvo && IsWindow(topo))
                    temp.Push(topo);
            }
            while (temp.Count > 0)
            {
                _pilhaModais.Push(temp.Pop());
            }
        }

        private static void ReabilitarTodasJanelasDaThread(IntPtr hWndIgnorar)
        {
            if (_mainWindowHwnd != IntPtr.Zero && IsWindow(_mainWindowHwnd))
            {
                EnableWindow(_mainWindowHwnd, true);
            }

            uint threadId = GetCurrentThreadId();
            EnumThreadWindows(threadId, (hWnd, lParam) =>
            {
                if (hWnd != hWndIgnorar && IsWindow(hWnd) && IsWindowVisible(hWnd))
                {
                    EnableWindow(hWnd, true);
                }
                return true;
            }, IntPtr.Zero);

            if (_mainWindowHwnd != IntPtr.Zero && IsWindow(_mainWindowHwnd))
            {
                SetForegroundWindow(_mainWindowHwnd);
                SetActiveWindow(_mainWindowHwnd);
            }
        }

        private static void CentralizarSobreJanelaPai(AppWindow appWindow, IntPtr hWndPai)
        {
            if (hWndPai != IntPtr.Zero && GetWindowRect(hWndPai, out RECT rectPai))
            {
                int largPai = rectPai.Right - rectPai.Left;
                int altPai = rectPai.Bottom - rectPai.Top;
                int largFilho = appWindow.Size.Width;
                int altFilho = appWindow.Size.Height;

                int posX = rectPai.Left + Math.Max(0, (largPai - largFilho) / 2);
                int posY = rectPai.Top + Math.Max(0, (altPai - altFilho) / 2);

                appWindow.Move(new PointInt32(posX, posY));
            }
        }

        private static void LimparPilhaJanelasInvalidas()
        {
            while (_pilhaModais.Count > 0 && !IsWindow(_pilhaModais.Peek()))
            {
                IntPtr invalida = _pilhaModais.Pop();
                _mapaFilhoParaPai.Remove(invalida);
            }
        }

        private static bool VerificarSeChamadorEhJanelaRaiz(out bool ehMainWindow)
        {
            ehMainWindow = false;
            var stack = new StackTrace();
            for (int i = 1; i < Math.Min(stack.FrameCount, 6); i++)
            {
                var tipo = stack.GetFrame(i)?.GetMethod()?.DeclaringType;
                if (tipo == null) continue;

                string nome = tipo.Name;
                if (nome.Equals("MainWindow", StringComparison.OrdinalIgnoreCase))
                {
                    ehMainWindow = true;
                    return true;
                }
                if (nome.Equals("LoginWindow", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}