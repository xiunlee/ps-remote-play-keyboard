using System;
using System.Collections.Generic;
using System.Globalization;

namespace PSKeyboard
{
    // Immutable main-window catalogues. UiText owns process-local language selection.
    // Brand names, command-line options, hotkeys and format arguments stay unchanged.
    internal static class PreviewText
    {
        private static readonly string[] Languages = { "en", "zh-Hant", "zh-Hans", "ja", "ko", "de", "fr", "es", "pt-BR" };
        private static readonly Dictionary<string, Dictionary<string, string>> Catalogues = Build();

        internal static string[] SupportedLanguageTags { get { return (string[])Languages.Clone(); } }
        internal static string[] ResourceKeys
        {
            get
            {
                string[] keys = new string[Catalogues["en"].Count];
                Catalogues["en"].Keys.CopyTo(keys, 0);
                Array.Sort(keys, StringComparer.Ordinal);
                return keys;
            }
        }

        internal static string T(string key)
        {
            if (String.IsNullOrEmpty(key)) return String.Empty;
            Dictionary<string, string> table;
            string value;
            if (Catalogues.TryGetValue(UiText.LanguageTag, out table) && table.TryGetValue(key, out value)) return value;
            return Catalogues["en"].TryGetValue(key, out value) ? value : key;
        }

        internal static string F(string key, params object[] args)
        {
            return String.Format(CultureInfo.CurrentCulture, T(key), args ?? new object[0]);
        }

        internal static bool HasTranslation(string language, string key)
        {
            if (String.IsNullOrEmpty(language) || String.IsNullOrEmpty(key)) return false;
            Dictionary<string, string> table;
            return Catalogues.TryGetValue(language, out table) && table.ContainsKey(key);
        }

        private static void Add(Dictionary<string, Dictionary<string, string>> tables, string key, params string[] values)
        {
            if (values.Length != Languages.Length) throw new InvalidOperationException("Incomplete preview translation row: " + key);
            for (int i = 0; i < Languages.Length; ++i)
            {
                if (String.IsNullOrWhiteSpace(values[i])) throw new InvalidOperationException("Empty preview translation: " + key);
                tables[Languages[i]].Add(key, values[i]);
            }
        }

        private static Dictionary<string, Dictionary<string, string>> Build()
        {
            Dictionary<string, Dictionary<string, string>> tables = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            foreach (string language in Languages) tables.Add(language, new Dictionary<string, string>(StringComparer.Ordinal));
            const string command = "PSKeyboard-DualSensePreview.exe [--persona dualsensegamepadv5] [--status-file FULL_JSON_PATH_INSIDE_THIS_PACKAGE]";

            Add(tables, "ModeLabel",
                "DualSense · Standard · Gamepad only", "DualSense 一般版 · 純手把", "DualSense 标准版 · 仅手柄",
                "DualSense 通常版 · ゲームパッドのみ", "DualSense 일반형 · 게임패드 전용", "DualSense · Standard · Nur Gamepad",
                "DualSense · Standard · Manette seule", "DualSense · Estándar · Solo mando", "DualSense · Padrão · Somente controle");

            Add(tables, "Usage",
                "Usage: " + command + "\r\nConnect the controller after opening the app, then press F8 to enable the keyboard.",
                "用法：" + command + "\r\n開啟後連接手把，再按 F8 啟用鍵盤。",
                "用法：" + command + "\r\n打开后连接手柄，再按 F8 启用键盘。",
                "使い方：" + command + "\r\nアプリを開いてコントローラーを接続し、F8 でキーボード操作を有効にしてください。",
                "사용법: " + command + "\r\n앱을 열어 컨트롤러를 연결한 뒤 F8을 눌러 키보드 조작을 켜세요.",
                "Aufruf: " + command + "\r\nNach dem Öffnen den Controller verbinden, dann mit F8 die Tastatursteuerung aktivieren.",
                "Utilisation : " + command + "\r\nOuvrez l’application, connectez la manette, puis appuyez sur F8 pour activer le clavier.",
                "Uso: " + command + "\r\nAbre la aplicación, conecta el mando y pulsa F8 para activar el teclado.",
                "Uso: " + command + "\r\nAbra o aplicativo, conecte o controle e pressione F8 para ativar o teclado.");

            Add(tables, "AlreadyRunning",
                "Another version of 上班不要玩 is still running.\r\nClose it normally before opening this version. Keep Sony Remote Play open.",
                "另一版「上班不要玩」仍在執行。\r\n請先正常關閉它，再開本版本；Sony Remote Play 請保持開啟。",
                "另一个版本的“上班不要玩”仍在运行。\r\n请先正常关闭它，再打开本版本；请保持 Sony Remote Play 开启。",
                "別のバージョンの「上班不要玩」が起動中です。\r\n先に通常の手順で終了してください。Sony Remote Play は開いたままにしてください。",
                "다른 버전의 上班不要玩이 실행 중입니다.\r\n먼저 정상 종료한 뒤 이 버전을 여세요. Sony Remote Play는 계속 켜 두세요.",
                "Eine andere Version von 上班不要玩 läuft noch.\r\nBeende sie regulär, bevor du diese Version öffnest. Lass Sony Remote Play geöffnet.",
                "Une autre version de 上班不要玩 est encore ouverte.\r\nFermez-la normalement avant d’ouvrir celle-ci. Gardez Sony Remote Play ouvert.",
                "Otra versión de 上班不要玩 sigue abierta.\r\nCiérrala normalmente antes de abrir esta versión. Mantén Sony Remote Play abierto.",
                "Outra versão de 上班不要玩 ainda está aberta.\r\nFeche-a normalmente antes de abrir esta versão. Mantenha o Sony Remote Play aberto.");

            Add(tables, "Eyebrow",
                "KEYBOARD / REMOTE PLAY", "鍵盤 / REMOTE PLAY", "键盘 / REMOTE PLAY",
                "キーボード / REMOTE PLAY", "키보드 / REMOTE PLAY", "TASTATUR / REMOTE PLAY",
                "CLAVIER / REMOTE PLAY", "TECLADO / REMOTE PLAY", "TECLADO / REMOTE PLAY");

            Add(tables, "ModeHeading",
                "Controller mode", "手把模式", "手柄模式",
                "コントローラーモード", "컨트롤러 모드", "Controller-Modus",
                "Mode de manette", "Modo de mando", "Modo do controle");

            Add(tables, "ConnectionHeading",
                "Controller connection", "手把連線", "手柄连接",
                "コントローラー接続", "컨트롤러 연결", "Controller-Verbindung",
                "Connexion de la manette", "Conexión del mando", "Conexão do controle");

            Add(tables, "Disconnected",
                "Not connected\r\nUse the button below to connect the controller.", "尚未連接\r\n點選下方按鈕連接手把。", "尚未连接\r\n点击下方按钮连接手柄。",
                "未接続\r\n下のボタンでコントローラーを接続してください。", "연결되지 않음\r\n아래 버튼으로 컨트롤러를 연결하세요.", "Nicht verbunden\r\nVerbinde den Controller über die Schaltfläche unten.",
                "Non connectée\r\nUtilisez le bouton ci-dessous pour connecter la manette.", "Sin conexión\r\nUsa el botón de abajo para conectar el mando.", "Não conectado\r\nUse o botão abaixo para conectar o controle.");

            Add(tables, "InitiallyPaused",
                "Paused\r\nConnect first, then press F8 to enable.", "已暫停\r\n連接後按 F8 啟用。", "已暂停\r\n连接后按 F8 启用。",
                "一時停止中\r\n接続後、F8 で有効にします。", "일시 정지\r\n연결 후 F8을 눌러 켜세요.", "Pausiert\r\nErst verbinden, dann mit F8 aktivieren.",
                "En pause\r\nConnectez la manette, puis activez avec F8.", "En pausa\r\nConecta el mando y activa con F8.", "Pausado\r\nConecte o controle e ative com F8.");

            Add(tables, "SettingsButton",
                "Keys and auto-repeat", "按鍵與連點設定", "按键与连发设置",
                "キー・連射設定", "키 및 연사 설정", "Tasten und Dauerfeuer",
                "Touches et répétition", "Teclas y repetición", "Teclas e repetição");

            Add(tables, "ConnectButton",
                "Connect controller", "連接手把", "连接手柄",
                "コントローラーを接続", "컨트롤러 연결", "Controller verbinden",
                "Connecter la manette", "Conectar mando", "Conectar controle");

            Add(tables, "DisconnectButton",
                "Disconnect controller", "中斷手把", "断开手柄",
                "コントローラーを切断", "컨트롤러 연결 해제", "Controller trennen",
                "Déconnecter la manette", "Desconectar mando", "Desconectar controle");

            Add(tables, "BindingsHeading",
                "Your key bindings", "你的按鍵配置", "你的按键配置",
                "キー割り当て", "내 키 설정", "Deine Tastenbelegung",
                "Vos touches", "Tus teclas", "Suas teclas");

            Add(tables, "LeftStick",
                "Left stick", "左搖桿", "左摇杆",
                "左スティック", "왼쪽 스틱", "Linker Stick",
                "Stick gauche", "Stick izquierdo", "Analógico esquerdo");

            Add(tables, "ControlsHelp",
                "F8 enables or pauses control. Input works only in the official Remote Play foreground window. Switching away stops auto-repeat; press the key again after returning.\r\nCustomize all 22 actions, with Off, Hold or Toggle auto-repeat for each key.",
                "F8 隨時啟用／暫停；僅在官方 Remote Play 前景視窗輸入。切換視窗會停止連點，返回後需重新按鍵。\r\n22 個動作可自訂按鍵；各鍵可設定關閉、按住或切換連點。",
                "F8 随时启用／暂停；仅在官方 Remote Play 前台窗口输入。切换窗口会停止连发，返回后需重新按键。\r\n22 个动作可自定义按键；各键可设置关闭、按住或切换连发。",
                "F8 で操作を有効／一時停止。入力は公式 Remote Play が最前面のときのみ有効です。別の画面に切り替えると連射が停止し、戻ったらキーを押し直す必要があります。\r\n22 個の動作にキーを割り当て、連射をオフ・押している間・押すたび切替から選べます。",
                "F8로 조작을 켜거나 일시 정지합니다. 공식 Remote Play가 맨 앞에 있을 때만 입력됩니다. 창을 전환하면 연사가 멈추며, 돌아온 뒤 키를 다시 눌러야 합니다.\r\n22개 동작의 키를 바꾸고 각 키의 연사를 끄기·누르는 동안·누를 때마다 전환으로 설정하세요.",
                "F8 aktiviert oder pausiert die Steuerung. Eingaben gelten nur, wenn das offizielle Remote Play im Vordergrund ist. Ein Fensterwechsel stoppt das Dauerfeuer; drücke die Taste danach erneut.\r\nAlle 22 Aktionen sind belegbar, mit Dauerfeuer aus, beim Halten oder zum Umschalten.",
                "F8 active ou suspend les commandes. Seule la fenêtre officielle Remote Play au premier plan reçoit les entrées. Changer de fenêtre arrête la répétition ; appuyez de nouveau sur la touche au retour.\r\nPersonnalisez 22 actions : répétition désactivée, par maintien ou par bascule.",
                "F8 activa o pausa los controles. Solo funcionan con Remote Play oficial en primer plano. Cambiar de ventana detiene la repetición; al volver, pulsa la tecla de nuevo.\r\nPersonaliza 22 acciones y elige repetición desactivada, al mantener o al alternar.",
                "F8 ativa ou pausa os comandos. A entrada só funciona com o Remote Play oficial em primeiro plano. Trocar de janela interrompe a repetição; ao voltar, pressione a tecla novamente.\r\nPersonalize 22 ações, com repetição desativada, ao segurar ou ao alternar.");

            Add(tables, "GameSuitability",
                "Suited to 2D games, especially JRPGs. Not suitable for 3D games that require flexible camera control.",
                "適合 2D 遊戲，尤其是 JRPG；不適合需要靈活視角控制的 3D 遊戲。",
                "适合 2D 游戏，尤其是 JRPG；不适合需要灵活视角控制的 3D 游戏。",
                "2D ゲーム、特に JRPG に向いています。自由なカメラ操作が必要な 3D ゲームには向いていません。",
                "2D 게임, 특히 JRPG에 적합합니다. 자유로운 시점 조작이 필요한 3D 게임에는 적합하지 않습니다.",
                "Gut geeignet für 2D-Spiele, besonders JRPGs. Nicht geeignet für 3D-Spiele, die eine flexible Kamerasteuerung erfordern.",
                "Adapté aux jeux 2D, surtout aux JRPG. Ne convient pas aux jeux 3D qui demandent une grande liberté de contrôle de la caméra.",
                "Adecuado para juegos 2D, especialmente JRPG. No es adecuado para juegos 3D que requieren un control flexible de la cámara.",
                "Adequado para jogos 2D, especialmente JRPGs. Não é indicado para jogos 3D que exigem controle livre da câmera.");

            Add(tables, "ProfilePreserved",
                "Your saved keys and auto-repeat settings are kept. They change only when you choose Save.", "沿用你的個人按鍵與連點設定，只有按下儲存才會更新。", "沿用你的个人按键与连发设置，只有点击保存才会更新。",
                "保存済みのキー・連射設定を引き継ぎます。「保存」を選ぶまで変更されません。", "저장된 키 및 연사 설정을 유지합니다. 저장을 선택할 때만 변경됩니다.", "Deine Tasten- und Dauerfeuer-Einstellungen bleiben erhalten. Nur Speichern übernimmt Änderungen.",
                "Vos touches et réglages de répétition sont conservés. Ils changent uniquement lorsque vous enregistrez.", "Se conservan tus teclas y ajustes de repetición. Solo cambian al guardar.", "Suas teclas e configurações de repetição são mantidas. Só mudam quando você salva.");

            Add(tables, "ProfileSaved",
                "Settings saved. Return to Remote Play and press F8 to enable control.", "設定已儲存；切回 Remote Play，再按 F8 啟用。", "设置已保存；切回 Remote Play，再按 F8 启用。",
                "設定を保存しました。Remote Play に戻り、F8 で操作を有効にしてください。", "설정을 저장했습니다. Remote Play로 돌아가 F8을 눌러 조작을 켜세요.", "Einstellungen gespeichert. Kehre zu Remote Play zurück und aktiviere die Steuerung mit F8.",
                "Réglages enregistrés. Revenez à Remote Play et activez les commandes avec F8.", "Ajustes guardados. Vuelve a Remote Play y activa los controles con F8.", "Configurações salvas. Volte ao Remote Play e ative os comandos com F8.");

            Add(tables, "FreeOpenSource",
                "Free and open source · Author: xiunlee · Donations are optional and never affect features.", "免費開源 · 作者 xiunlee · 贊助完全自願，不影響任何功能。", "免费开源 · 作者 xiunlee · 赞助完全自愿，不影响任何功能。",
                "無料・オープンソース · 作者 xiunlee · 支援は任意で、機能に影響しません。", "무료 오픈 소스 · 제작자 xiunlee · 후원은 선택이며 기능에 영향을 주지 않습니다.", "Kostenlos und quelloffen · Autor: xiunlee · Spenden sind freiwillig und ändern keine Funktionen.",
                "Gratuit et open source · Auteur : xiunlee · Les dons sont facultatifs, sans effet sur les fonctions.", "Gratis y de código abierto · Autor: xiunlee · Donar es opcional y no afecta a las funciones.", "Grátis e de código aberto · Autor: xiunlee · Doações são opcionais e não afetam os recursos.");

            Add(tables, "CatsSupport",
                "Finding it useful? You're welcome to donate US$1 toward a can of food for our three cats.", "覺得好用的話，歡迎贊助 US$1，幫我們的三隻貓咪加個罐頭。", "觉得好用的话，欢迎赞助 US$1，帮我们的三只猫咪加个罐头。",
                "気に入っていただけたら、3 匹の猫たちのごはん代に US$1 のご支援をいただけるとうれしいです。", "유용하게 쓰셨다면 고양이 세 마리의 캔 간식을 위해 US$1을 후원해 주세요.", "Wenn dir die App gefällt, freuen sich unsere drei Katzen über US$1 für eine Dose Futter.",
                "L’application vous plaît ? Un don de US$1 aiderait à offrir une boîte de pâtée à nos trois chats.", "Si te resulta útil, puedes aportar US$1 para comprar comida en lata para nuestros tres gatos.", "Se o aplicativo foi útil, você pode doar US$1 para ajudar a comprar uma lata de comida para nossos três gatos.");

            Add(tables, "SponsorButton",
                "Treat the cats · US$1", "贊助貓咪 US$1", "赞助猫咪 US$1",
                "猫たちを支援 · US$1", "고양이 후원 · US$1", "Für die Katzen · US$1",
                "Pour les chats · US$1", "Para los gatos · US$1", "Para os gatos · US$1");

            Add(tables, "FeedbackButton",
                "Send feedback", "意見回饋", "意见反馈",
                "フィードバック", "의견 보내기", "Feedback senden",
                "Donner un avis", "Enviar comentarios", "Enviar feedback");

            Add(tables, "SponsorUnavailable",
                "The donation link is not available yet.", "贊助連結尚未開放。", "赞助链接尚未开放。",
                "支援リンクはまだ利用できません。", "후원 링크가 아직 열리지 않았습니다.", "Der Spendenlink ist noch nicht verfügbar.",
                "Le lien de don n’est pas encore disponible.", "El enlace para aportar aún no está disponible.", "O link de doação ainda não está disponível.");

            Add(tables, "SponsorFailure",
                "Could not open the PayPal donation page. Please try again later.", "無法開啟 PayPal 贊助頁，請稍後再試。", "无法打开 PayPal 赞助页面，请稍后再试。",
                "PayPal の支援ページを開けませんでした。しばらくしてからお試しください。", "PayPal 후원 페이지를 열 수 없습니다. 나중에 다시 시도하세요.", "Die PayPal-Spendenseite konnte nicht geöffnet werden. Versuche es später erneut.",
                "Impossible d’ouvrir la page de don PayPal. Réessayez plus tard.", "No se pudo abrir la página de PayPal. Inténtalo más tarde.", "Não foi possível abrir a página de doação do PayPal. Tente novamente mais tarde.");

            Add(tables, "FeedbackFailure",
                "Could not open your email app. You can email {0} yourself.", "無法開啟郵件程式。你可以自行寄信至 {0}。", "无法打开邮件应用。你可以自行发送邮件至 {0}。",
                "メールアプリを開けませんでした。{0} 宛てに直接お送りいただけます。", "메일 앱을 열 수 없습니다. {0} 주소로 직접 메일을 보내 주세요.", "Das E-Mail-Programm konnte nicht geöffnet werden. Schreibe direkt an {0}.",
                "Impossible d’ouvrir votre messagerie. Vous pouvez écrire directement à {0}.", "No se pudo abrir tu aplicación de correo. Puedes escribir directamente a {0}.", "Não foi possível abrir seu aplicativo de e-mail. Você pode escrever diretamente para {0}.");

            Add(tables, "CleanupSuffix",
                "\r\nCleanup could not be fully confirmed. Reconnection is blocked.", "\r\n清理未完全確認，已鎖住重新連接。", "\r\n清理尚未完全确认，已禁止重新连接。",
                "\r\n後処理の完了を確認できません。再接続は無効になっています。", "\r\n정리 완료를 확인할 수 없어 재연결을 차단했습니다.", "\r\nDie Bereinigung ist nicht vollständig bestätigt. Erneutes Verbinden ist gesperrt.",
                "\r\nLe nettoyage n’a pas pu être confirmé. La reconnexion est bloquée.", "\r\nNo se pudo confirmar la limpieza. La reconexión está bloqueada.", "\r\nNão foi possível confirmar a limpeza. A reconexão está bloqueada.");

            Add(tables, "CleanupBlocked",
                "Cleanup could not be fully confirmed. Reconnection is blocked.\r\nKeep the local diagnostics and avoid creating another device.",
                "清理未完全確認，已鎖住重新連接。\r\n請保留本機診斷，不要重複建立裝置。",
                "清理尚未完全确认，已禁止重新连接。\r\n请保留本地诊断，不要重复创建设备。",
                "後処理の完了を確認できないため、再接続は無効です。\r\nローカル診断を保存し、別のデバイスを作成しないでください。",
                "정리 완료를 확인할 수 없어 재연결을 차단했습니다.\r\n로컬 진단 자료를 보관하고 장치를 다시 만들지 마세요.",
                "Die Bereinigung ist nicht vollständig bestätigt. Erneutes Verbinden ist gesperrt.\r\nBewahre die lokalen Diagnosedaten auf und erstelle kein weiteres Gerät.",
                "Le nettoyage n’a pas pu être confirmé. La reconnexion est bloquée.\r\nConservez les diagnostics locaux et ne créez pas d’autre périphérique.",
                "No se pudo confirmar la limpieza. La reconexión está bloqueada.\r\nConserva los diagnósticos locales y no crees otro dispositivo.",
                "Não foi possível confirmar a limpeza. A reconexão está bloqueada.\r\nGuarde os diagnósticos locais e não crie outro dispositivo.");

            Add(tables, "KeyboardPaused",
                "Paused\r\nPress F8 to enable control.", "已暫停\r\n按 F8 開始控制。", "已暂停\r\n按 F8 开始控制。",
                "一時停止中\r\nF8 で操作を有効にします。", "일시 정지\r\nF8을 눌러 조작을 켜세요.", "Pausiert\r\nMit F8 Steuerung aktivieren.",
                "En pause\r\nAppuyez sur F8 pour activer les commandes.", "En pausa\r\nPulsa F8 para activar los controles.", "Pausado\r\nPressione F8 para ativar os comandos.");

            Add(tables, "KeyboardWaiting",
                "Waiting for input\r\nReturn to Remote Play, release modifier keys, then press a mapped key.",
                "等待輸入\r\n切回 Remote Play、放開修飾鍵，再按下已設定的按鍵。",
                "等待输入\r\n切回 Remote Play、松开修饰键，再按下已设置的按键。",
                "入力待ち\r\nRemote Play に戻り、修飾キーを離してから割り当てたキーを押してください。",
                "입력 대기\r\nRemote Play로 돌아가 보조 키를 놓고 지정한 키를 누르세요.",
                "Warte auf Eingabe\r\nKehre zu Remote Play zurück, lasse Zusatztasten los und drücke eine belegte Taste.",
                "En attente d’entrée\r\nRevenez à Remote Play, relâchez les touches de modification, puis appuyez sur une touche attribuée.",
                "Esperando entrada\r\nVuelve a Remote Play, suelta las teclas modificadoras y pulsa una tecla asignada.",
                "Aguardando entrada\r\nVolte ao Remote Play, solte as teclas modificadoras e pressione uma tecla configurada.");

            Add(tables, "KeyboardActive",
                "Keyboard control active\r\nPress F8 to pause at any time.", "鍵盤控制中\r\n按 F8 隨時暫停。", "键盘控制中\r\n随时按 F8 暂停。",
                "キーボード操作中\r\nF8 でいつでも一時停止できます。", "키보드 조작 중\r\nF8로 언제든 일시 정지할 수 있습니다.", "Tastatursteuerung aktiv\r\nMit F8 jederzeit pausieren.",
                "Clavier actif\r\nAppuyez sur F8 pour mettre en pause.", "Control por teclado activo\r\nPulsa F8 para pausar en cualquier momento.", "Controle pelo teclado ativo\r\nPressione F8 para pausar a qualquer momento.");

            Add(tables, "PreviousRemovalUnknown",
                "The previous USB removal is not confirmed. A new device will not be created. Check the attachment location in the local diagnostics.",
                "上次 USB 移除尚未確認，暫不建立新裝置；請先核對本機診斷中的接入位置。",
                "上次 USB 移除尚未确认，暂不创建新设备；请先核对本地诊断中的接入位置。",
                "前回の USB 切断を確認できません。新しいデバイスは作成しません。ローカル診断で接続先を確認してください。",
                "이전 USB 장치 제거를 확인할 수 없어 새 장치를 만들지 않습니다. 로컬 진단에서 연결 위치를 확인하세요.",
                "Die letzte USB-Entfernung ist nicht bestätigt. Es wird kein neues Gerät erstellt. Prüfe den Anschlussort in den lokalen Diagnosedaten.",
                "Le retrait USB précédent n’est pas confirmé. Aucun nouveau périphérique ne sera créé. Vérifiez le point de connexion dans les diagnostics locaux.",
                "No se ha confirmado la retirada del USB anterior. No se creará otro dispositivo. Revisa el punto de conexión en los diagnósticos locales.",
                "A remoção USB anterior não foi confirmada. Nenhum novo dispositivo será criado. Confira o ponto de conexão nos diagnósticos locais.");

            Add(tables, "ControllerDisconnected",
                "Controller disconnected\r\nKeyboard control is paused.", "手把已中斷\r\n鍵盤控制已暫停。", "手柄已断开\r\n键盘控制已暂停。",
                "コントローラー切断済み\r\nキーボード操作は一時停止中です。", "컨트롤러 연결 해제됨\r\n키보드 조작이 일시 정지되었습니다.", "Controller getrennt\r\nDie Tastatursteuerung ist pausiert.",
                "Manette déconnectée\r\nLes commandes au clavier sont en pause.", "Mando desconectado\r\nEl control por teclado está en pausa.", "Controle desconectado\r\nOs comandos pelo teclado estão pausados.");

            Add(tables, "CleanupIncomplete",
                "Cleanup could not be fully confirmed. Reconnection is blocked.", "清理未完全確認，已鎖住重新連接。", "清理尚未完全确认，已禁止重新连接。",
                "後処理の完了を確認できません。再接続は無効になっています。", "정리 완료를 확인할 수 없어 재연결을 차단했습니다.", "Die Bereinigung ist nicht vollständig bestätigt. Erneutes Verbinden ist gesperrt.",
                "Le nettoyage n’a pas pu être confirmé. La reconnexion est bloquée.", "No se pudo confirmar la limpieza. La reconexión está bloqueada.", "Não foi possível confirmar a limpeza. A reconexão está bloqueada.");

            Add(tables, "Connecting",
                "Connecting…\r\n{0}", "正在連接…\r\n{0}", "正在连接…\r\n{0}",
                "接続中…\r\n{0}", "연결 중…\r\n{0}", "Verbindung wird hergestellt…\r\n{0}",
                "Connexion…\r\n{0}", "Conectando…\r\n{0}", "Conectando…\r\n{0}");

            Add(tables, "ControllerAttached",
                "Controller attached in Windows\r\n{0}", "Windows 手把已接入\r\n{0}", "Windows 手柄已接入\r\n{0}",
                "Windows に接続済み\r\n{0}", "Windows에 컨트롤러 연결됨\r\n{0}", "Controller in Windows verbunden\r\n{0}",
                "Manette connectée à Windows\r\n{0}", "Mando conectado en Windows\r\n{0}", "Controle conectado no Windows\r\n{0}");

            Add(tables, "ConnectionScope",
                "The connection indicator shows the local Windows device state. It does not confirm that PS5 has received input.",
                "連線燈號只表示本機 Windows 裝置狀態，不代表 PS5 已收到操作。",
                "连接指示灯仅表示本机 Windows 设备状态，不代表 PS5 已收到操作。",
                "接続ランプは Windows 上のデバイスの状態を示します。PS5 が入力を受け取ったことを確認するものではありません。",
                "연결 표시는 이 Windows 장치의 상태만 나타냅니다. PS5가 입력을 받았다는 뜻은 아닙니다.",
                "Die Anzeige zeigt den lokalen Windows-Gerätestatus. Sie bestätigt nicht, dass die PS5 Eingaben empfangen hat.",
                "Le voyant indique l’état du périphérique Windows local. Il ne confirme pas que la PS5 a reçu une commande.",
                "El indicador muestra el estado del dispositivo en Windows. No confirma que PS5 haya recibido una entrada.",
                "O indicador mostra o estado do dispositivo no Windows. Ele não confirma que o PS5 recebeu um comando.");

            Add(tables, "FailureKeyboardSetup",
                "F8 or keyboard capture could not start. Close other versions of this keyboard app, and keep Sony Remote Play open.",
                "F8 或鍵盤擷取無法啟用。請先關閉其他版本的鍵盤程式，保留 Sony Remote Play。",
                "F8 或键盘捕获无法启用。请先关闭其他版本的键盘程序，保持 Sony Remote Play 开启。",
                "F8 またはキー入力の取得を有効にできません。他のバージョンを閉じ、Sony Remote Play は開いたままにしてください。",
                "F8 또는 키보드 입력 감지를 시작할 수 없습니다. 이 키보드 앱의 다른 버전을 닫고 Sony Remote Play는 켜 두세요.",
                "F8 oder die Tastaturerfassung konnte nicht starten. Schließe andere Versionen dieser App und lass Sony Remote Play geöffnet.",
                "Impossible d’activer F8 ou la capture du clavier. Fermez les autres versions de cette application et gardez Sony Remote Play ouvert.",
                "No se pudo activar F8 o la captura del teclado. Cierra otras versiones de esta aplicación y mantén Sony Remote Play abierto.",
                "Não foi possível ativar F8 ou a captura do teclado. Feche outras versões deste aplicativo e mantenha o Sony Remote Play aberto.");

            Add(tables, "FailureUsbipMissing",
                "Installed USBip could not be found. This app does not install drivers automatically.", "找不到已安裝的 USBip；本程式不會自動安裝驅動。", "找不到已安装的 USBip；本程序不会自动安装驱动。",
                "インストール済みの USBip が見つかりません。このアプリはドライバーを自動でインストールしません。", "설치된 USBip를 찾을 수 없습니다. 이 앱은 드라이버를 자동으로 설치하지 않습니다.", "Die USBip-Installation wurde nicht gefunden. Diese App installiert keine Treiber automatisch.",
                "Installation USBip introuvable. Cette application n’installe pas de pilotes automatiquement.", "No se encontró una instalación de USBip. Esta aplicación no instala controladores automáticamente.", "A instalação do USBip não foi encontrada. Este aplicativo não instala drivers automaticamente.");

            Add(tables, "FailureUsbipInvalid",
                "USBip 0.9.8.1 components could not be verified. No driver was replaced or downgraded.", "USBip 0.9.8.1 元件驗證失敗，沒有更換或降級驅動。", "USBip 0.9.8.1 组件验证失败，未更换或降级驱动。",
                "USBip 0.9.8.1 の構成要素を検証できません。ドライバーの交換やダウングレードは行っていません。", "USBip 0.9.8.1 구성 요소를 검증하지 못했습니다. 드라이버를 교체하거나 다운그레이드하지 않았습니다.", "USBip 0.9.8.1 konnte nicht verifiziert werden. Kein Treiber wurde ersetzt oder herabgestuft.",
                "Impossible de vérifier les composants USBip 0.9.8.1. Aucun pilote n’a été remplacé ni rétrogradé.", "No se pudieron verificar los componentes de USBip 0.9.8.1. No se sustituyó ningún controlador ni se instaló una versión anterior.", "Não foi possível verificar os componentes do USBip 0.9.8.1. Nenhum driver foi substituído ou revertido.");

            Add(tables, "FailureExistingImport",
                "A previous controller connection is still attached. A new device was not created.", "發現先前手把通道仍有接入，已停止建立新裝置。", "发现先前的手柄仍处于连接状态，已停止创建新设备。",
                "前のコントローラー接続が残っています。新しいデバイスは作成していません。", "이전 컨트롤러가 아직 연결되어 있습니다. 새 장치를 만들지 않았습니다.", "Eine frühere Controller-Verbindung besteht noch. Es wurde kein neues Gerät erstellt.",
                "Une connexion de manette précédente est encore active. Aucun nouveau périphérique n’a été créé.", "Sigue conectado un mando de una sesión anterior. No se creó otro dispositivo.", "Uma conexão anterior do controle ainda está ativa. Nenhum novo dispositivo foi criado.");

            Add(tables, "FailureBackendInvalid",
                "A controller component is missing or could not be verified.", "手把元件遺失或驗證失敗。", "手柄组件缺失或验证失败。",
                "コントローラーの構成要素が見つからないか、検証に失敗しました。", "컨트롤러 구성 요소가 없거나 검증하지 못했습니다.", "Eine Controller-Komponente fehlt oder konnte nicht verifiziert werden.",
                "Un composant de la manette est manquant ou n’a pas pu être vérifié.", "Falta un componente del mando o no se pudo verificar.", "Um componente do controle está ausente ou não pôde ser verificado.");

            Add(tables, "FailurePortsUnavailable",
                "The connection ports are unavailable. The controller connection could not be completed.", "連線通道無法使用，未能完成手把連接。", "连接端口不可用，未能完成手柄连接。",
                "接続ポートを利用できません。コントローラーの接続を完了できませんでした。", "연결 포트를 사용할 수 없어 컨트롤러 연결을 완료하지 못했습니다.", "Die Verbindungsports sind nicht verfügbar. Die Controller-Verbindung konnte nicht abgeschlossen werden.",
                "Les ports de connexion sont indisponibles. La connexion de la manette n’a pas pu aboutir.", "Los puertos no están disponibles. No se pudo completar la conexión del mando.", "As portas não estão disponíveis. Não foi possível concluir a conexão do controle.");

            Add(tables, "FailureUsbAttach",
                "The USB controller attachment could not be confirmed. Keyboard control remains paused.", "USB 手把接入未能確認；尚未啟用鍵盤。", "无法确认 USB 手柄接入；键盘控制仍处于暂停状态。",
                "USB コントローラーの接続を確認できません。キーボード操作は一時停止のままです。", "USB 컨트롤러 연결을 확인하지 못했습니다. 키보드 조작은 일시 정지 상태입니다.", "Die USB-Controller-Verbindung konnte nicht bestätigt werden. Die Tastatursteuerung bleibt pausiert.",
                "La connexion de la manette USB n’a pas pu être confirmée. Le clavier reste en pause.", "No se pudo confirmar la conexión del mando USB. El teclado sigue en pausa.", "Não foi possível confirmar a conexão do controle USB. O teclado continua pausado.");

            Add(tables, "FailureSend",
                "The virtual controller output connection was interrupted. Keyboard control is paused.", "虛擬手把輸出通道已中斷，鍵盤已暫停。", "虚拟手柄输出连接已中断，键盘控制已暂停。",
                "仮想コントローラーの出力接続が切れました。キーボード操作を一時停止しました。", "가상 컨트롤러 출력 연결이 끊겨 키보드 조작을 일시 정지했습니다.", "Die Ausgabeverbindung des virtuellen Controllers wurde unterbrochen. Die Tastatursteuerung ist pausiert.",
                "La connexion de sortie de la manette virtuelle a été interrompue. Le clavier est en pause.", "Se interrumpió la conexión de salida del mando virtual. El teclado está en pausa.", "A conexão de saída do controle virtual foi interrompida. O teclado está pausado.");

            Add(tables, "FailureCancelled",
                "The operation was cancelled.", "操作已取消。", "操作已取消。",
                "操作をキャンセルしました。", "작업을 취소했습니다.", "Der Vorgang wurde abgebrochen.",
                "L’opération a été annulée.", "Se canceló la operación.", "A operação foi cancelada.");

            Add(tables, "FailureBackendStart",
                "The controller component could not start. Keyboard control remains paused.", "手把元件無法啟動，鍵盤控制維持暫停。", "手柄组件无法启动，键盘控制保持暂停。",
                "コントローラーの構成要素を起動できません。キーボード操作は一時停止のままです。", "컨트롤러 구성 요소를 시작할 수 없습니다. 키보드 조작은 일시 정지 상태입니다.", "Die Controller-Komponente konnte nicht starten. Die Tastatursteuerung bleibt pausiert.",
                "Le composant de la manette n’a pas pu démarrer. Le clavier reste en pause.", "No se pudo iniciar el componente del mando. El teclado sigue en pausa.", "Não foi possível iniciar o componente do controle. O teclado continua pausado.");

            Add(tables, "FailureBackendExited",
                "The controller component stopped unexpectedly. Keyboard control remains paused.", "手把元件意外停止，鍵盤控制維持暫停。", "手柄组件意外停止，键盘控制保持暂停。",
                "コントローラーの構成要素が予期せず停止しました。キーボード操作は一時停止のままです。", "컨트롤러 구성 요소가 예기치 않게 중지되었습니다. 키보드 조작은 일시 정지 상태입니다.", "Die Controller-Komponente wurde unerwartet beendet. Die Tastatursteuerung bleibt pausiert.",
                "Le composant de la manette s’est arrêté de façon inattendue. Le clavier reste en pause.", "El componente del mando se detuvo inesperadamente. El teclado sigue en pausa.", "O componente do controle parou inesperadamente. O teclado continua pausado.");

            Add(tables, "FailureUsbipTimeout",
                "The USBip operation timed out. Keyboard control remains paused; check the local diagnostics before reconnecting.",
                "USBip 操作逾時，鍵盤控制維持暫停；重新連接前請先核對本機診斷。",
                "USBip 操作超时，键盘控制保持暂停；重新连接前请先检查本地诊断。",
                "USBip の操作がタイムアウトしました。キーボード操作は一時停止中です。再接続の前にローカル診断を確認してください。",
                "USBip 작업 시간이 초과되었습니다. 키보드 조작은 일시 정지 상태입니다. 재연결 전에 로컬 진단을 확인하세요.",
                "Zeitüberschreitung bei USBip. Die Tastatursteuerung bleibt pausiert. Prüfe vor dem erneuten Verbinden die lokalen Diagnosedaten.",
                "L’opération USBip a expiré. Le clavier reste en pause. Vérifiez les diagnostics locaux avant de reconnecter.",
                "La operación de USBip agotó el tiempo de espera. El teclado sigue en pausa; revisa los diagnósticos locales antes de reconectar.",
                "A operação do USBip excedeu o tempo limite. O teclado continua pausado; confira os diagnósticos locais antes de reconectar.");

            Add(tables, "FailureUsbipOutput",
                "The USBip response exceeded the allowed size. The connection was stopped.", "USBip 回應超過允許大小，已停止連接。", "USBip 响应超过允许大小，已停止连接。",
                "USBip の応答がサイズ上限を超えたため、接続を停止しました。", "USBip 응답이 허용 크기를 초과하여 연결을 중단했습니다.", "Die USBip-Antwort war zu groß. Die Verbindung wurde gestoppt.",
                "La réponse USBip dépasse la taille autorisée. La connexion a été arrêtée.", "La respuesta de USBip superó el tamaño permitido. Se detuvo la conexión.", "A resposta do USBip excedeu o tamanho permitido. A conexão foi interrompida.");

            Add(tables, "FailureConnect",
                "Could not create the virtual controller. Check the local diagnostic status.", "建立模擬手把失敗。請查看本機診斷狀態。", "创建虚拟手柄失败。请查看本地诊断状态。",
                "仮想コントローラーを作成できませんでした。ローカル診断の状態を確認してください。", "가상 컨트롤러를 만들지 못했습니다. 로컬 진단 상태를 확인하세요.", "Der virtuelle Controller konnte nicht erstellt werden. Prüfe den lokalen Diagnosestatus.",
                "Impossible de créer la manette virtuelle. Consultez l’état des diagnostics locaux.", "No se pudo crear el mando virtual. Consulta el estado de los diagnósticos locales.", "Não foi possível criar o controle virtual. Confira o estado dos diagnósticos locais.");

            Add(tables, "FeedbackSubjectFmt",
                "上班不要玩 {0} feedback", "上班不要玩 {0} 意見回饋", "上班不要玩 {0} 意见反馈",
                "上班不要玩 {0} フィードバック", "上班不要玩 {0} 의견", "上班不要玩 {0} Feedback",
                "上班不要玩 {0} — avis", "上班不要玩 {0} — comentarios", "上班不要玩 {0} — feedback");

            return tables;
        }
    }
}
