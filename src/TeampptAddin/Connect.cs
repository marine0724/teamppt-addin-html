using System;
using System.Runtime.InteropServices;
using Extensibility;
using Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace TeampptAddin
{
    /// <summary>
    /// COM Add-in 진입점. 직접 패널을 만들지 않고 전부 TaskPaneManager에 위임한다.
    /// - IDTExtensibility2: 생명주기. OnConnection에서 앱 이벤트 구독.
    /// - ICustomTaskPaneConsumer: CTPFactoryAvailable에서 팩토리를 Manager에 보관(생성 안 함).
    /// - IRibbonExtensibility: TEAMPPT 탭 토글 버튼. 버튼이 활성창 패널을 토글.
    /// LoadBehavior=3 유지(리본 표시). 자동 생성만 제거.
    /// </summary>
    [ComVisible(true)]
    [Guid("7B3A4D1E-9F2C-4A85-B6D0-3E8F1C5A7B92")]
    [ProgId("TeampptAddin.Connect")]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class Connect : IDTExtensibility2, ICustomTaskPaneConsumer, IRibbonExtensibility
    {
        private PowerPoint.Application _app;
        private readonly TaskPaneManager _manager = new TaskPaneManager();

        #region IDTExtensibility2

        public void OnConnection(object Application, ext_ConnectMode ConnectMode,
            object AddInInst, ref Array custom)
        {
            _app = (PowerPoint.Application)Application;
            Globals.Application = _app;

            _app.WindowActivate += App_WindowActivate;
            _app.PresentationClose += App_PresentationClose;
            Logger.Log("Connect.OnConnection: events wired");
        }

        public void OnDisconnection(ext_DisconnectMode RemoveMode, ref Array custom)
        {
            try
            {
                if (_app != null)
                {
                    _app.WindowActivate -= App_WindowActivate;
                    _app.PresentationClose -= App_PresentationClose;
                }
            }
            catch (Exception ex) { Logger.Log($"OnDisconnection unwire failed: {ex.Message}"); }

            _manager.ReleaseAll();
            _app = null;
            Globals.Application = null;
        }

        public void OnAddInsUpdate(ref Array custom) { }

        public void OnStartupComplete(ref Array custom) { }

        public void OnBeginShutdown(ref Array custom)
        {
            _manager.ReleaseAll();
        }

        #endregion

        #region App events

        private void App_WindowActivate(PowerPoint.Presentation Pres, PowerPoint.DocumentWindow Wn)
        {
            _manager.SweepClosedWindows();
            // 공유 리본은 창 전환 시 getPressed를 자동 재평가하지 않는다. 활성창 기준으로 버튼을 갱신.
            _manager.RefreshButton();
        }

        private void App_PresentationClose(PowerPoint.Presentation Pres)
        {
            _manager.SweepClosedWindows();
        }

        #endregion

        #region ICustomTaskPaneConsumer

        // 자동 생성 제거: 팩토리만 보관(idempotent). 패널은 버튼으로만 생성.
        public void CTPFactoryAvailable(ICTPFactory CTPFactoryInst)
        {
            _manager.SetFactory(CTPFactoryInst);
        }

        #endregion

        #region IRibbonExtensibility

        public string GetCustomUI(string RibbonID)
        {
            return RibbonXml;
        }

        public void OnRibbonLoad(IRibbonUI ribbon)
        {
            _manager.SetRibbon(ribbon);
        }

        public void OnToggleAction(IRibbonControl control, bool pressed)
        {
            _manager.Toggle(ActiveHwnd(), pressed);
        }

        public bool GetTogglePressed(IRibbonControl control)
        {
            return _manager.IsVisible(ActiveHwnd());
        }

        // 커스텀 브랜드 아이콘 (Assets/teamppt-icon.png). 1회 로드 후 캐시.
        private static System.Drawing.Image _iconImage;
        private static stdole.IPictureDisp _iconPic;

        public stdole.IPictureDisp GetButtonImage(IRibbonControl control)
        {
            try
            {
                if (_iconPic == null)
                {
                    var path = System.IO.Path.Combine(Globals.AssetsDir, "teamppt-icon.png");
                    var bytes = System.IO.File.ReadAllBytes(path);
                    _iconImage = System.Drawing.Image.FromStream(new System.IO.MemoryStream(bytes));
                    _iconPic = RibbonImage.Convert(_iconImage);
                }
                return _iconPic;
            }
            catch (Exception ex)
            {
                Logger.Log($"GetButtonImage failed: {ex.Message}");
                return null;
            }
        }

        // System.Drawing.Image -> stdole.IPictureDisp 변환 (AxHost 보호 헬퍼 노출).
        private sealed class RibbonImage : System.Windows.Forms.AxHost
        {
            private RibbonImage() : base("4f2f3c5a-1b2d-4e6f-9a8b-0c1d2e3f4a5b") { }
            public static stdole.IPictureDisp Convert(System.Drawing.Image image)
                => (stdole.IPictureDisp)GetIPictureDispFromPicture(image);
        }

        private int ActiveHwnd()
        {
            try { return _app?.ActiveWindow?.HWND ?? 0; }
            catch { return 0; }
        }

        // 전용 탭이 아니라 홈 탭(TabHome) 끝에 그룹 1개를 끼워 넣는다 (Plus AI 스타일).
        private const string RibbonXml =
@"<customUI xmlns='http://schemas.microsoft.com/office/2009/07/customui' onLoad='OnRibbonLoad'>
  <ribbon>
    <tabs>
      <tab idMso='TabHome'>
        <group id='teampptGroup' label='TEAMPPT'>
          <toggleButton id='teampptToggle' label='TEAMPPT 패널'
                        size='large' getImage='GetButtonImage'
                        screentip='TEAMPPT 패널 열기/닫기'
                        onAction='OnToggleAction' getPressed='GetTogglePressed'/>
        </group>
      </tab>
    </tabs>
  </ribbon>
</customUI>";

        #endregion
    }
}
