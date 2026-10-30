using System;
using System.Collections.Generic;
using System.Globalization;

namespace PSKeyboard
{
    // All catalogues are constructed once and never modified after publication.
    // Language selection is process-local; no Windows setting is changed.
    internal static class UiText
    {
        private static readonly string[] Languages = { "en", "zh-Hant", "zh-Hans", "ja", "ko", "de", "fr", "es", "pt-BR" };
        private static readonly Dictionary<string, Dictionary<string, string>> Catalogues = Build();
        private static volatile string selectedLanguage = ResolveLanguage(CultureInfo.CurrentUICulture);
        internal static string LanguageTag { get { return selectedLanguage; } }
        internal static string[] SupportedLanguageTags { get { return (string[])Languages.Clone(); } }
        internal static string[] ResourceKeys
        {
            get { string[] keys = new string[Catalogues["en"].Count]; Catalogues["en"].Keys.CopyTo(keys, 0); return keys; }
        }
        internal static void Initialize(CultureInfo culture) { selectedLanguage = ResolveLanguage(culture); }
        internal static string ResolveLanguage(CultureInfo culture)
        {
            if (culture == null) return "en";
            string name = culture.Name.ToLowerInvariant();
            if (name == "zh" || name.StartsWith("zh-", StringComparison.Ordinal))
            {
                if (name.Contains("hant") || name == "zh-tw" || name == "zh-hk" || name == "zh-mo") return "zh-Hant";
                return "zh-Hans";
            }
            string language = culture.TwoLetterISOLanguageName.ToLowerInvariant();
            if (language == "pt") return "pt-BR";
            foreach (string supported in Languages) if (supported == language) return supported;
            return "en";
        }
        internal static string T(string key)
        {
            if (String.IsNullOrEmpty(key)) return String.Empty;
            string value;
            string language = selectedLanguage;
            if (Catalogues[language].TryGetValue(key, out value)) return value;
            return Catalogues["en"].TryGetValue(key, out value) ? value : key;
        }
        internal static string ActionName(int index)
        {
            if (index < 0 || index >= 22) throw new ArgumentOutOfRangeException("index");
            return T("Action" + index.ToString(CultureInfo.InvariantCulture));
        }
        internal static bool HasTranslation(string language, string key)
        {
            Dictionary<string, string> table;
            return Catalogues.TryGetValue(language, out table) && table.ContainsKey(key);
        }
        internal static string KeyName(int virtualKey)
        {
            if (virtualKey == 0) return T("Unassigned");
            if ((virtualKey >= 0x30 && virtualKey <= 0x39) || (virtualKey >= 0x41 && virtualKey <= 0x5a))
                return ((char)virtualKey).ToString();
            if (virtualKey >= 0x70 && virtualKey <= 0x87)
                return "F" + (virtualKey - 0x6f).ToString(CultureInfo.InvariantCulture);
            if (virtualKey >= 0x60 && virtualKey <= 0x69)
                return T("KeyNumPad") + " " + (virtualKey - 0x60).ToString(CultureInfo.InvariantCulture);
            switch (virtualKey)
            {
                case 0x08: return T("KeyBackspace"); case 0x09: return T("KeyTab");
                case 0x0d: return T("KeyEnter"); case 0x1b: return T("KeyEscape");
                case 0x20: return T("KeySpace"); case 0x21: return T("KeyPageUp");
                case 0x22: return T("KeyPageDown"); case 0x23: return T("KeyEnd");
                case 0x24: return T("KeyHome"); case 0x25: return "←"; case 0x26: return "↑";
                case 0x27: return "→"; case 0x28: return "↓"; case 0x2d: return T("KeyInsert");
                case 0x2e: return T("KeyDelete"); case 0x6a: return T("KeyNumPad") + " ×";
                case 0x6b: return T("KeyNumPad") + " +"; case 0x6d: return T("KeyNumPad") + " −";
                case 0x6e: return T("KeyNumPad") + " ."; case 0x6f: return T("KeyNumPad") + " ÷";
                // OEM virtual keys depend on the keyboard layout. A numeric key
                // label avoids pretending every layout has the same punctuation.
                default: return T("KeyOther") + " " + virtualKey.ToString(CultureInfo.InvariantCulture);
            }
        }
        private static void Add(Dictionary<string, Dictionary<string, string>> tables, string key, params string[] values)
        {
            if (values.Length != Languages.Length) throw new InvalidOperationException("Incomplete translation row.");
            for (int index = 0; index < Languages.Length; index++)
            {
                if (String.IsNullOrWhiteSpace(values[index])) throw new InvalidOperationException("Empty translation.");
                tables[Languages[index]].Add(key, values[index]);
            }
        }
        private static Dictionary<string, Dictionary<string, string>> Build()
        {
            Dictionary<string, Dictionary<string, string>> tables = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            foreach (string language in Languages) tables.Add(language, new Dictionary<string, string>(StringComparer.Ordinal));
            Add(tables, "AppTitle", "Three Cats Keybridge", "上班不要玩", "上班不要玩", "Three Cats Keybridge", "Three Cats Keybridge", "Three Cats Keybridge", "Three Cats Keybridge", "Three Cats Keybridge", "Three Cats Keybridge");
            Add(tables, "AppSubtitle", "Your keyboard. Your controller.", "用鍵盤，操控你的手把。", "用键盘，操控你的手柄。", "キーボードで、コントローラー操作。", "키보드로 컨트롤러를 조작하세요.", "Deine Tastatur. Dein Controller.", "Votre clavier, votre manette.", "Tu teclado, tu mando.", "Seu teclado, seu controle.");
            Add(tables, "ControllerTitle", "Controller data", "手把資料", "手柄数据", "コントローラーデータ", "컨트롤러 데이터", "Controller-Daten", "Données de la manette", "Datos del mando", "Dados do controle");
            Add(tables, "ControllerDisconnected", "Not connected", "尚未連接", "尚未连接", "未接続", "연결되지 않음", "Nicht verbunden", "Non connectée", "Sin conexión", "Não conectado");
            Add(tables, "ControllerWaiting", "Waiting for controller", "等待手把連接", "等待手柄连接", "接続を待っています", "컨트롤러 연결 대기 중", "Warte auf Controller", "En attente de la manette", "Esperando el mando", "Aguardando o controle");
            Add(tables, "ControllerVerifying", "Checking controller data", "正在確認手把資料", "正在确认手柄数据", "データを確認中", "컨트롤러 데이터 확인 중", "Daten werden geprüft", "Vérification des données", "Comprobando los datos", "Verificando os dados");
            Add(tables, "ControllerReady", "Receiving data normally", "手把資料正常", "手柄数据正常", "データ受信中", "데이터 정상 수신 중", "Datenempfang normal", "Réception normale", "Recepción normal", "Recepção normal");
            Add(tables, "ControllerStale", "Waiting for fresh data", "暫時沒有新資料", "暂时没有新数据", "新しいデータを待っています", "새 데이터 대기 중", "Warte auf neue Daten", "En attente de nouvelles données", "Esperando nuevos datos", "Aguardando novos dados");
            Add(tables, "ControllerFailure", "Connection interrupted", "手把連線中斷", "手柄连接中断", "接続が切断されました", "연결이 끊어졌습니다", "Verbindung unterbrochen", "Connexion interrompue", "Conexión interrumpida", "Conexão interrompida");
            Add(tables, "KeyboardTitle", "Keyboard control", "鍵盤控制", "键盘控制", "キーボード操作", "키보드 조작", "Tastatursteuerung", "Contrôle au clavier", "Control con teclado", "Controle pelo teclado");
            Add(tables, "KeyboardPaused", "Paused", "已暫停", "已暂停", "一時停止中", "일시 정지", "Pausiert", "En pause", "En pausa", "Em pausa");
            Add(tables, "KeyboardActive", "Enabled", "已啟用", "已启用", "有効", "사용 중", "Aktiv", "Activé", "Activado", "Ativado");
            Add(tables, "KeyboardFocusPaused", "Paused outside Remote Play", "已暫停 · 切回 Remote Play", "已暂停 · 切回 Remote Play", "一時停止中 · Remote Play に戻る", "일시 정지 · Remote Play로 전환", "Außerhalb von Remote Play pausiert", "En pause hors de Remote Play", "En pausa fuera de Remote Play", "Em pausa fora do Remote Play");
            Add(tables, "EnableKeyboard", "Enable keyboard · F8", "啟用鍵盤 · F8", "启用键盘 · F8", "キーボードを有効化 · F8", "키보드 사용 · F8", "Tastatur aktivieren · F8", "Activer le clavier · F8", "Activar teclado · F8", "Ativar teclado · F8");
            Add(tables, "PauseKeyboard", "Pause keyboard · F8", "暫停鍵盤 · F8", "暂停键盘 · F8", "キーボードを一時停止 · F8", "키보드 일시 정지 · F8", "Tastatur pausieren · F8", "Mettre en pause · F8", "Pausar teclado · F8", "Pausar teclado · F8");
            Add(tables, "ConfigureKeys", "Customize keys", "自訂按鍵", "自定义按键", "キー設定", "키 설정", "Tasten anpassen", "Personnaliser les touches", "Personalizar teclas", "Personalizar teclas");
            Add(tables, "AdvancedShow", "Connection settings", "連線設定", "连接设置", "接続設定", "연결 설정", "Verbindungseinstellungen", "Paramètres de connexion", "Ajustes de conexión", "Configurações de conexão");
            Add(tables, "AdvancedHide", "Hide connection settings", "收起連線設定", "收起连接设置", "接続設定を閉じる", "연결 설정 닫기", "Einstellungen ausblenden", "Masquer les paramètres", "Ocultar ajustes", "Ocultar configurações");
            Add(tables, "BusId", "Controller location", "手把連接位置", "手柄连接位置", "コントローラーの接続先", "컨트롤러 연결 위치", "Controller-Anschluss", "Emplacement de la manette", "Ubicación del mando", "Localização do controle");
            Add(tables, "Product", "Controller model", "手把型號", "手柄型号", "コントローラーの機種", "컨트롤러 모델", "Controller-Modell", "Modèle de manette", "Modelo de mando", "Modelo do controle");
            Add(tables, "Endpoint", "Input channel", "資料通道", "数据通道", "入力チャンネル", "입력 채널", "Eingangskanal", "Canal d’entrée", "Canal de entrada", "Canal de entrada");
            Add(tables, "ProductConfirmed", "DUALSHOCK 4 · 05C4 (tested)", "DUALSHOCK 4 · 05C4（已實測）", "DUALSHOCK 4 · 05C4（已实测）", "DUALSHOCK 4 · 05C4（動作確認済み）", "DUALSHOCK 4 · 05C4 (테스트 완료)", "DUALSHOCK 4 · 05C4 (getestet)", "DUALSHOCK 4 · 05C4 (testée)", "DUALSHOCK 4 · 05C4 (probado)", "DUALSHOCK 4 · 05C4 (testado)");
            Add(tables, "ProductExperimental", "DUALSHOCK 4 · 09CC (experimental)", "DUALSHOCK 4 · 09CC（實驗支援）", "DUALSHOCK 4 · 09CC（实验支持）", "DUALSHOCK 4 · 09CC（試験対応）", "DUALSHOCK 4 · 09CC (실험적 지원)", "DUALSHOCK 4 · 09CC (experimentell)", "DUALSHOCK 4 · 09CC (expérimentale)", "DUALSHOCK 4 · 09CC (experimental)", "DUALSHOCK 4 · 09CC (experimental)");
            Add(tables, "StartRelay", "Connect relay", "連接中繼", "连接中继", "中継に接続", "중계 연결", "Weiterleitung verbinden", "Connecter le relais", "Conectar el enlace", "Conectar o intermediário");
            Add(tables, "StopRelay", "Disconnect relay", "中斷中繼", "断开中继", "中継を切断", "중계 연결 해제", "Weiterleitung trennen", "Déconnecter le relais", "Desconectar el enlace", "Desconectar o intermediário");
            Add(tables, "StopConfirmTitle", "Disconnect controller?", "要中斷手把連線嗎？", "要断开手柄连接吗？", "コントローラーを切断しますか？", "컨트롤러 연결을 해제할까요?", "Controller trennen?", "Déconnecter la manette ?", "¿Desconectar el mando?", "Desconectar o controle?");
            Add(tables, "StopConfirmMessage", "This interrupts controller input. The physical controller may need to be restored afterward. Pause your game first.", "這會中斷手把輸入，之後可能需要還原實體手把。請先暫停遊戲。", "这会中断手柄输入，之后可能需要还原实体手柄。请先暂停游戏。", "コントローラー入力が中断されます。その後、実機の復元が必要な場合があります。先にゲームを一時停止してください。", "컨트롤러 입력이 중단됩니다. 이후 실제 컨트롤러를 복원해야 할 수 있습니다. 먼저 게임을 일시 정지하세요.", "Die Controller-Eingabe wird unterbrochen. Danach muss der physische Controller eventuell wiederhergestellt werden. Pausiere zuerst das Spiel.", "Cela interrompt les commandes de la manette. Une restauration de la manette physique peut ensuite être nécessaire. Mettez d’abord le jeu en pause.", "Se interrumpirá la entrada del mando. Después puede ser necesario restaurar el mando físico. Pausa primero el juego.", "A entrada do controle será interrompida. Depois, talvez seja necessário restaurar o controle físico. Pause o jogo primeiro.");
            Add(tables, "ReadyHint", "F8: enable/pause. Keys work only in active official Remote Play. Switching away stops repeats; press again after returning. Light: data, not PS5 connection.", "F8 啟用／暫停；僅在官方 Remote Play 為目前視窗時輸入。切換視窗停止連發，返回需重新按鍵。燈號代表手把資料，不代表 PS5 連線。", "F8 启用／暂停；仅在官方 Remote Play 为当前窗口时输入。切换窗口停止连发，返回需重新按键。指示灯代表手柄数据，不代表 PS5 连接。", "F8 で有効／一時停止。入力は公式 Remote Play が手前のときのみ。別画面に移ると連射は停止し、戻ったら再度キーを押す必要があります。ランプはデータ受信状態で、PS5 の接続状態ではありません。", "F8: 사용/일시 정지. 공식 Remote Play 창이 활성 상태일 때만 입력합니다. 다른 창으로 전환하면 연사가 멈추며, 돌아온 뒤 다시 눌러야 합니다. 표시등은 데이터 수신 상태이며 PS5 연결 상태가 아닙니다.", "F8: ein/pausieren. Nur im aktiven offiziellen Remote Play. Fensterwechsel stoppt Dauerfeuer; danach erneut drücken. Anzeige: Daten, keine PS5-Verbindung.", "F8 : activer/pause. Uniquement dans Remote Play officiel actif. Changer de fenêtre arrête la répétition ; rappuyez au retour. Voyant : données, pas connexion PS5.", "F8: activar/pausar. Solo en Remote Play oficial activo. Cambiar de ventana detiene la repetición; pulsa de nuevo al volver. Indicador: datos, no conexión PS5.", "F8: ativar/pausar. Só no Remote Play oficial ativo. Mudar de janela encerra a repetição; pressione de novo ao voltar. Indicador: dados, não conexão PS5.");
            Add(tables, "ExperimentalHint", "05C4 has been tested on this PC. 09CC is not yet verified. Other controllers are not supported.", "05C4 已在本機實測；09CC 尚未驗證。目前不支援其他手把。", "05C4 已在本机实测；09CC 尚未验证。目前不支持其他手柄。", "05C4 はこの PC で動作確認済みです。09CC は未確認です。他のコントローラーには対応していません。", "05C4는 이 PC에서 테스트했습니다. 09CC는 아직 검증되지 않았습니다. 다른 컨트롤러는 지원하지 않습니다.", "05C4 wurde auf diesem PC getestet. 09CC ist noch nicht geprüft. Andere Controller werden nicht unterstützt.", "Le modèle 05C4 a été testé sur ce PC. Le modèle 09CC reste à vérifier. Les autres manettes ne sont pas prises en charge.", "El modelo 05C4 se ha probado en este PC. El 09CC aún no está verificado. No se admiten otros mandos.", "O modelo 05C4 foi testado neste PC. O 09CC ainda não foi verificado. Outros controles não são compatíveis.");
            Add(tables, "ProfileLoadWarning", "Saved keys could not be loaded. Defaults are active; your file was left unchanged.", "無法載入已儲存的按鍵，暫時使用預設設定；原設定檔保留不變。", "无法加载已保存的按键，暂时使用默认设置；原配置文件保持不变。", "保存したキー設定を読み込めませんでした。標準設定を使用します。元のファイルは変更していません。", "저장된 키 설정을 불러오지 못했습니다. 기본 설정을 사용하며 기존 파일은 그대로 유지됩니다.", "Gespeicherte Tasten konnten nicht geladen werden. Standardwerte sind aktiv; die Datei bleibt unverändert.", "Impossible de charger les touches enregistrées. Les valeurs par défaut sont actives ; le fichier reste inchangé.", "No se pudieron cargar las teclas guardadas. Se usan los valores predeterminados; el archivo no se ha modificado.", "Não foi possível carregar as teclas salvas. Os padrões estão ativos; seu arquivo não foi alterado.");
            Add(tables, "KeyboardSetupFailure", "Keyboard control could not start. Close another mapper using F8, then try again.", "無法啟用鍵盤控制。請確認沒有其他程式占用 F8，再重試。", "无法启用键盘控制。请确认没有其他程序占用 F8，再重试。", "キーボード操作を開始できません。F8 を使用する他のアプリを終了してから再試行してください。", "키보드 조작을 시작하지 못했습니다. F8을 사용하는 다른 프로그램을 닫고 다시 시도하세요.", "Tastatursteuerung konnte nicht starten. Schließe andere Programme, die F8 verwenden, und versuche es erneut.", "Impossible d’activer le clavier. Fermez les autres programmes utilisant F8, puis réessayez.", "No se pudo activar el teclado. Cierra otros programas que utilicen F8 e inténtalo de nuevo.", "Não foi possível ativar o teclado. Feche outros programas que usam F8 e tente novamente.");
            Add(tables, "RelayStartupFailure", "The relay could not start. Check the connection settings or stop the previous session.", "無法啟動中繼，請檢查連線設定或先停止上一個中繼。", "无法启动中继，请检查连接设置或先停止上一个中继。", "中継を開始できません。接続設定を確認するか、前の中継を終了してください。", "중계를 시작하지 못했습니다. 연결 설정을 확인하거나 이전 중계를 종료하세요.", "Die Weiterleitung konnte nicht starten. Prüfe die Einstellungen oder beende die vorherige Sitzung.", "Impossible de démarrer le relais. Vérifiez les paramètres ou arrêtez la session précédente.", "No se pudo iniciar el enlace. Revisa los ajustes o detén la sesión anterior.", "Não foi possível iniciar a conexão. Confira as configurações ou encerre a sessão anterior.");
            Add(tables, "AlreadyRunning", "The app is already running.", "程式已經開啟。", "程序已经开启。", "アプリはすでに起動しています。", "앱이 이미 실행 중입니다.", "Die App läuft bereits.", "L’application est déjà ouverte.", "La aplicación ya está abierta.", "O aplicativo já está aberto.");
            Add(tables, "InvalidArguments", "The startup options are invalid.", "啟動參數不正確。", "启动参数不正确。", "起動オプションが正しくありません。", "시작 옵션이 올바르지 않습니다.", "Die Startoptionen sind ungültig.", "Les options de démarrage sont incorrectes.", "Las opciones de inicio no son válidas.", "As opções de inicialização são inválidas.");
            Add(tables, "BindingsTitle", "Customize your keys", "自訂鍵盤按鍵", "自定义键盘按键", "キーをカスタマイズ", "키보드 키 설정", "Tasten individuell anpassen", "Personnalisez vos touches", "Personaliza tus teclas", "Personalize suas teclas");
            Add(tables, "BindingsSubtitle", "Double-click an action, then press your new key.", "連按兩下手把動作，再按下要使用的新按鍵。", "双击手柄操作，再按下要使用的新按键。", "操作をダブルクリックしてから、新しいキーを押してください。", "동작을 두 번 클릭한 다음 새 키를 누르세요.", "Doppelklicke auf eine Aktion und drücke die neue Taste.", "Double-cliquez sur une action, puis appuyez sur la nouvelle touche.", "Haz doble clic en una acción y pulsa la nueva tecla.", "Clique duas vezes em uma ação e pressione a nova tecla.");
            Add(tables, "ActionColumn", "Controller action", "手把動作", "手柄操作", "コントローラー操作", "컨트롤러 동작", "Controller-Aktion", "Action de la manette", "Acción del mando", "Ação do controle");
            Add(tables, "KeyColumn", "Keyboard key", "鍵盤按鍵", "键盘按键", "キーボードのキー", "키보드 키", "Tastaturtaste", "Touche du clavier", "Tecla del teclado", "Tecla do teclado");
            Add(tables, "CaptureKey", "Press a key", "按下新按鍵", "按下新按键", "キーを設定", "키 지정", "Taste zuweisen", "Attribuer une touche", "Asignar una tecla", "Atribuir uma tecla");
            Add(tables, "CapturePrompt", "Press one key now. F8 and modifier keys are reserved.", "請按下一個按鍵。F8 與修飾鍵保留使用。", "请按下一个按键。F8 和修饰键保留使用。", "キーを 1 つ押してください。F8 と修飾キーは使用できません。", "키 하나를 누르세요. F8과 보조 키는 지정할 수 없습니다.", "Drücke eine einzelne Taste. F8 und Modifikatortasten sind reserviert.", "Appuyez sur une seule touche. F8 et les touches de modification sont réservées.", "Pulsa una sola tecla. F8 y las teclas modificadoras están reservadas.", "Pressione uma única tecla. F8 e as teclas modificadoras são reservadas.");
            Add(tables, "ClearBinding", "Clear key", "清除按鍵", "清除按键", "割り当てを解除", "키 지정 해제", "Taste entfernen", "Effacer la touche", "Quitar tecla", "Remover tecla");
            Add(tables, "RestoreDefaults", "Restore defaults", "恢復預設", "恢复默认", "標準設定に戻す", "기본값 복원", "Standardwerte", "Valeurs par défaut", "Restaurar valores", "Restaurar padrões");
            Add(tables, "Save", "Save changes", "儲存設定", "保存设置", "保存", "설정 저장", "Änderungen speichern", "Enregistrer", "Guardar cambios", "Salvar alterações");
            Add(tables, "Cancel", "Cancel", "取消", "取消", "キャンセル", "취소", "Abbrechen", "Annuler", "Cancelar", "Cancelar");
            Add(tables, "BindingConflict", "That key is already assigned. Clear its current action first.", "這個按鍵已被使用，請先清除原本的對應動作。", "这个按键已被使用，请先清除原来的对应操作。", "このキーは使用中です。先に現在の割り当てを解除してください。", "이미 지정된 키입니다. 기존 동작의 키 지정을 먼저 해제하세요.", "Diese Taste ist bereits belegt. Entferne zuerst die bisherige Zuordnung.", "Cette touche est déjà attribuée. Effacez d’abord son affectation actuelle.", "Esa tecla ya está asignada. Quita primero su asignación actual.", "Esta tecla já está atribuída. Remova primeiro a atribuição atual.");
            Add(tables, "BindingInvalid", "Choose a single keyboard key. F8, mouse buttons and modifier keys cannot be assigned.", "請選擇單一鍵盤按鍵；不能使用 F8、滑鼠鍵或修飾鍵。", "请选择单个键盘按键；不能使用 F8、鼠标键或修饰键。", "キーボードのキーを 1 つ選んでください。F8、マウスボタン、修飾キーは使用できません。", "키보드 키 하나를 선택하세요. F8, 마우스 버튼, 보조 키는 지정할 수 없습니다.", "Wähle eine einzelne Tastaturtaste. F8, Maustasten und Modifikatortasten sind nicht zulässig.", "Choisissez une seule touche du clavier. F8, les boutons de souris et les touches de modification ne sont pas autorisés.", "Elige una tecla del teclado. No se admiten F8, botones del ratón ni teclas modificadoras.", "Escolha uma tecla do teclado. F8, botões do mouse e teclas modificadoras não são permitidos.");
            Add(tables, "BindingsPausedHint", "Control stays paused. Return to official Remote Play and press F8 to resume.", "控制會維持暫停；切回官方 Remote Play，再按 F8 啟用。", "控制会保持暂停；切回官方 Remote Play，再按 F8 启用。", "操作は一時停止のままです。公式 Remote Play に戻り、F8 で有効にしてください。", "조작은 일시 정지 상태입니다. 공식 Remote Play로 돌아가 F8을 눌러 켜세요.", "Steuerung bleibt pausiert. Zurück zu offiziellem Remote Play wechseln und F8 drücken.", "Les commandes restent en pause. Revenez à Remote Play officiel et appuyez sur F8.", "El control sigue en pausa. Vuelve a Remote Play oficial y pulsa F8 para activarlo.", "O controle continua em pausa. Volte ao Remote Play oficial e pressione F8 para ativar.");
            Add(tables, "ProfileSaveFailure", "Settings could not be saved. Your active bindings were not changed.", "無法儲存設定，目前的按鍵設定未變更。", "无法保存设置，当前按键设置未更改。", "設定を保存できませんでした。現在のキー設定は変更していません。", "설정을 저장하지 못했습니다. 현재 키 설정은 변경되지 않았습니다.", "Einstellungen konnten nicht gespeichert werden. Die aktive Belegung bleibt unverändert.", "Impossible d’enregistrer. Les touches actives n’ont pas été modifiées.", "No se pudieron guardar los ajustes. Las teclas activas no han cambiado.", "Não foi possível salvar. As teclas ativas não foram alteradas.");
            Add(tables, "LanguageLabel", "Language follows Windows", "語言依 Windows 設定", "语言跟随 Windows 设置", "Windows の表示言語を使用", "Windows 표시 언어 사용", "Sprache wie in Windows", "Langue de Windows", "Idioma de Windows", "Idioma do Windows");
            Add(tables, "Unassigned", "Not assigned", "未設定", "未设置", "未設定", "지정되지 않음", "Nicht zugewiesen", "Non attribuée", "Sin asignar", "Não atribuída");
            Add(tables, "KeySpace", "Space", "空白鍵", "空格键", "スペース", "스페이스", "Leertaste", "Espace", "Espacio", "Espaço");
            Add(tables, "KeyEnter", "Enter", "Enter", "Enter", "Enter", "Enter", "Enter", "Entrée", "Intro", "Enter");
            Add(tables, "KeyEscape", "Esc", "Esc", "Esc", "Esc", "Esc", "Esc", "Échap", "Esc", "Esc");
            Add(tables, "KeyBackspace", "Backspace", "退格鍵", "退格键", "Backspace", "백스페이스", "Rücktaste", "Retour arrière", "Retroceso", "Backspace");
            Add(tables, "KeyTab", "Tab", "Tab", "Tab", "Tab", "Tab", "Tab", "Tab", "Tab", "Tab");
            Add(tables, "KeyDelete", "Delete", "Delete", "Delete", "Delete", "Delete", "Entf", "Suppr", "Supr", "Delete");
            Add(tables, "KeyInsert", "Insert", "Insert", "Insert", "Insert", "Insert", "Einfg", "Inser", "Insert", "Insert");
            Add(tables, "KeyHome", "Home", "Home", "Home", "Home", "Home", "Pos1", "Début", "Inicio", "Home");
            Add(tables, "KeyEnd", "End", "End", "End", "End", "End", "Ende", "Fin", "Fin", "End");
            Add(tables, "KeyPageUp", "Page Up", "Page Up", "Page Up", "Page Up", "Page Up", "Bild auf", "Page préc.", "Re Pág", "Page Up");
            Add(tables, "KeyPageDown", "Page Down", "Page Down", "Page Down", "Page Down", "Page Down", "Bild ab", "Page suiv.", "Av Pág", "Page Down");
            Add(tables, "KeyNumPad", "Numpad", "數字鍵盤", "数字键盘", "テンキー", "숫자 키패드", "Ziffernblock", "Pavé num.", "Teclado num.", "Teclado num.");
            Add(tables, "KeyOther", "Key", "按鍵", "按键", "キー", "키", "Taste", "Touche", "Tecla", "Tecla");
            Add(tables, "Action0", "Left stick ↑", "左搖桿 ↑", "左摇杆 ↑", "左スティック ↑", "왼쪽 스틱 ↑", "Linker Stick ↑", "Stick gauche ↑", "Stick izquierdo ↑", "Analógico esquerdo ↑");
            Add(tables, "Action1", "Left stick ↓", "左搖桿 ↓", "左摇杆 ↓", "左スティック ↓", "왼쪽 스틱 ↓", "Linker Stick ↓", "Stick gauche ↓", "Stick izquierdo ↓", "Analógico esquerdo ↓");
            Add(tables, "Action2", "Left stick ←", "左搖桿 ←", "左摇杆 ←", "左スティック ←", "왼쪽 스틱 ←", "Linker Stick ←", "Stick gauche ←", "Stick izquierdo ←", "Analógico esquerdo ←");
            Add(tables, "Action3", "Left stick →", "左搖桿 →", "左摇杆 →", "左スティック →", "왼쪽 스틱 →", "Linker Stick →", "Stick gauche →", "Stick izquierdo →", "Analógico esquerdo →");
            Add(tables, "Action4", "D-pad ↑", "方向鍵 ↑", "方向键 ↑", "方向キー ↑", "방향키 ↑", "Steuerkreuz ↑", "Croix directionnelle ↑", "Cruceta ↑", "Direcional ↑");
            Add(tables, "Action5", "D-pad ↓", "方向鍵 ↓", "方向键 ↓", "方向キー ↓", "방향키 ↓", "Steuerkreuz ↓", "Croix directionnelle ↓", "Cruceta ↓", "Direcional ↓");
            Add(tables, "Action6", "D-pad ←", "方向鍵 ←", "方向键 ←", "方向キー ←", "방향키 ←", "Steuerkreuz ←", "Croix directionnelle ←", "Cruceta ←", "Direcional ←");
            Add(tables, "Action7", "D-pad →", "方向鍵 →", "方向键 →", "方向キー →", "방향키 →", "Steuerkreuz →", "Croix directionnelle →", "Cruceta →", "Direcional →");
            Add(tables, "Action8", "×", "×", "×", "×", "×", "×", "×", "×", "×");
            Add(tables, "Action9", "○", "○", "○", "○", "○", "○", "○", "○", "○");
            Add(tables, "Action10", "□", "□", "□", "□", "□", "□", "□", "□", "□");
            Add(tables, "Action11", "△", "△", "△", "△", "△", "△", "△", "△", "△");
            Add(tables, "Action12", "L1", "L1", "L1", "L1", "L1", "L1", "L1", "L1", "L1");
            Add(tables, "Action13", "R1", "R1", "R1", "R1", "R1", "R1", "R1", "R1", "R1");
            Add(tables, "Action14", "L2", "L2", "L2", "L2", "L2", "L2", "L2", "L2", "L2");
            Add(tables, "Action15", "R2", "R2", "R2", "R2", "R2", "R2", "R2", "R2", "R2");
            Add(tables, "Action16", "Share", "分享（Share）", "分享（Share）", "SHARE", "공유 (Share)", "Share", "Partager (Share)", "Compartir (Share)", "Compartilhar (Share)");
            Add(tables, "Action17", "Options", "選項（Options）", "选项（Options）", "OPTIONS", "옵션 (Options)", "Options", "Options", "Opciones", "Opções");
            Add(tables, "Action18", "L3", "L3", "L3", "L3", "L3", "L3", "L3", "L3", "L3");
            Add(tables, "Action19", "R3", "R3", "R3", "R3", "R3", "R3", "R3", "R3", "R3");
            Add(tables, "Action20", "PS", "PS", "PS", "PS", "PS", "PS", "PS", "PS", "PS");
            Add(tables, "Action21", "Touchpad click", "觸控板按下", "触摸板按下", "タッチパッド押し込み", "터치패드 누르기", "Touchpad drücken", "Appui pavé tactile", "Pulsar panel táctil", "Pressionar touchpad");
            Add(tables, "RepeatTitle", "Auto-repeat", "連發設定", "连发设置", "連射設定", "연사 설정", "Dauerfeuer", "Répétition automatique", "Repetición automática", "Repetição automática");
            Add(tables, "RepeatModeLabel", "Mode", "模式", "模式", "モード", "모드", "Modus", "Mode", "Modo", "Modo");
            Add(tables, "RepeatOff", "Off", "關閉", "关闭", "オフ", "끄기", "Aus", "Désactivée", "Desactivada", "Desativada");
            Add(tables, "RepeatHold", "Hold", "按住連發", "按住连发", "押している間", "누르는 동안", "Gedrückt halten", "Maintenir", "Mantener", "Manter");
            Add(tables, "RepeatToggle", "Toggle", "按一下切換", "按一下切换", "押すたび切替", "눌러서 전환", "Umschalten", "Basculer", "Alternar", "Alternar");
            Add(tables, "RepeatIntervalLabel", "Interval (ms)", "間隔（毫秒）", "间隔（毫秒）", "間隔（ミリ秒）", "간격 (ms)", "Intervall (ms)", "Intervalle (ms)", "Intervalo (ms)", "Intervalo (ms)");
            Add(tables, "RepeatColumn", "Repeat", "連發", "连发", "連射", "연사", "Dauerfeuer", "Répétition", "Repetición", "Repetição");
            Add(tables, "RepeatHint", "Hold stops on release. Toggle stops on the next press. Leaving the Remote Play window stops all repeats; after returning, press again to restart. F8 pauses all.", "按住模式放開即停止；切換模式再按一下停止。離開 Remote Play 視窗會停止全部連發，返回後需重新按鍵；F8 全部暫停。", "按住模式松开即停止；切换模式再按一下停止。离开 Remote Play 窗口会停止全部连发，返回后需重新按键；F8 全部暂停。", "押している間は離すと停止、切替式はもう一度押すと停止します。Remote Play の画面を離れると全連射が停止し、戻ったら再度押す必要があります。F8 で全停止。", "누르는 동안 모드는 키를 놓으면, 전환 모드는 다시 누르면 멈춥니다. Remote Play 창을 벗어나면 모든 연사가 멈추며, 돌아온 뒤 다시 눌러야 합니다. F8은 전체 일시 정지입니다.", "Halten stoppt beim Loslassen, Umschalten beim nächsten Druck. Ein Fensterwechsel beendet alles; nach Rückkehr zu Remote Play erneut drücken. F8 pausiert alles.", "Maintenir s’arrête au relâchement ; Basculer, au prochain appui. Quitter la fenêtre Remote Play arrête toute répétition ; rappuyez au retour. F8 suspend tout.", "Mantener para al soltar; Alternar, al volver a pulsar. Salir de la ventana de Remote Play detiene toda repetición; pulsa de nuevo al volver. F8 pausa todo.", "Manter para ao soltar; Alternar, ao pressionar de novo. Sair da janela do Remote Play encerra toda repetição; pressione de novo ao voltar. F8 pausa tudo.");
            return tables;
        }
    }
}

