#!/usr/bin/env python3
"""Generate Mac's nine-language catalogue from reviewed Windows translations.

Only the small Add(tables, key, ...strings) subset is parsed; no C# is executed.
Mac-specific wording is explicit below. Run --check to verify the checked-in
catalogue, or --output PATH to produce a bundle resource from the same sources.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
LANGUAGES = ("en", "zh-Hant", "zh-Hans", "ja", "ko", "de", "fr", "es", "pt-BR")
SOURCES = ("src/physical-keyboard/UiText.cs", "research/dualsense-preview/PreviewText.cs")
DEFAULT_OUTPUT = ROOT / "assets/macos/strings.json"

# Rows follow LANGUAGES. Existing translations stay in C#; only Mac differences
# and new Mac UI copy belong here. Keys may also be English literal aliases.
MAC_ROWS = {
    "AppTitle": (
        "Three Cats Keybridge",
        "上班不要玩",
        "上班不要玩",
        "Three Cats Keybridge",
        "Three Cats Keybridge",
        "Three Cats Keybridge",
        "Three Cats Keybridge",
        "Three Cats Keybridge",
        "Three Cats Keybridge",
    ),
    "AppSubtitle": (
        "Keyboard mapping and virtual controllers for compatible Remote Play environments.",
        "鍵盤映射與模擬手把工具，適用相容的遠端遊玩環境。",
        "键盘映射与模拟手柄工具，适用于兼容的远程游玩环境。",
        "対応するリモートプレイ環境向けのキーマッピング・仮想コントローラーツール。",
        "호환되는 리모트 플레이 환경을 위한 키 매핑 및 가상 컨트롤러 도구입니다.",
        "Tastenbelegung und virtuelle Controller für kompatible Remote-Play-Umgebungen.",
        "Mappage du clavier et manettes virtuelles pour les environnements de jeu à distance compatibles.",
        "Asignación de teclas y mandos virtuales para entornos de juego remoto compatibles.",
        "Mapeamento de teclas e controles virtuais para ambientes de jogo remoto compatíveis.",
    ),
    "IndependentProject": (
        "Independent project. Not an official Sony product; not affiliated with or endorsed by Sony.",
        "獨立專案，非 Sony 官方產品，與 Sony 無隸屬或背書關係。",
        "独立项目，非 Sony 官方产品，与 Sony 无隶属或背书关系。",
        "独立したプロジェクトです。Sony の公式製品ではなく、Sony との提携や Sony からの推奨はありません。",
        "독립 프로젝트입니다. Sony의 공식 제품이 아니며 Sony와 제휴 관계가 없고 Sony의 공인이나 추천을 받지 않았습니다.",
        "Unabhängiges Projekt. Kein offizielles Sony-Produkt; weder mit Sony verbunden noch von Sony empfohlen.",
        "Projet indépendant. Ce n’est pas un produit officiel de Sony ; il n’est ni affilié à Sony ni approuvé par Sony.",
        "Proyecto independiente. No es un producto oficial de Sony ni está afiliado a Sony o respaldado por Sony.",
        "Projeto independente. Não é um produto oficial da Sony e não tem vínculo com a Sony nem seu endosso.",
    ),
    "ModeLabel": (
        "Virtual controller · Gamepad only",
        "模擬手把 · 純手把輸出",
        "模拟手柄 · 仅手柄输出",
        "仮想コントローラー · ゲームパッド出力のみ",
        "가상 컨트롤러 · 게임패드 출력 전용",
        "Virtueller Controller · Nur Gamepad-Ausgabe",
        "Manette virtuelle · Sortie manette uniquement",
        "Mando virtual · Solo salida de mando",
        "Controle virtual · Somente saída de controle",
    ),
    "ProductConfirmed": (
        "Virtual controller",
        "模擬手把",
        "模拟手柄",
        "仮想コントローラー",
        "가상 컨트롤러",
        "Virtueller Controller",
        "Manette virtuelle",
        "Mando virtual",
        "Controle virtual",
    ),
    "ProductExperimental": (
        "Virtual controller",
        "模擬手把",
        "模拟手柄",
        "仮想コントローラー",
        "가상 컨트롤러",
        "Virtueller Controller",
        "Manette virtuelle",
        "Mando virtual",
        "Controle virtual",
    ),
    "Creating the DualSense controller…": (
        "Creating the virtual controller…",
        "正在建立模擬手把…",
        "正在创建模拟手柄…",
        "仮想コントローラーを作成中…",
        "가상 컨트롤러 생성 중…",
        "Virtueller Controller wird erstellt…",
        "Création de la manette virtuelle…",
        "Creando el mando virtual…",
        "Criando o controle virtual…",
    ),
    "Usage instructions": ("Usage instructions", "操作說明", "操作说明", "使い方", "사용 방법", "Bedienung", "Mode d’emploi", "Instrucciones", "Instruções"),
    "Hide instructions": ("Hide instructions", "收起說明", "收起说明", "説明を閉じる", "설명 닫기", "Hinweise ausblenden", "Masquer les instructions", "Ocultar instrucciones", "Ocultar instruções"),
    "Appearance": ("Appearance", "外觀", "外观", "外観", "모양", "Darstellung", "Apparence", "Apariencia", "Aparência"),
    "System appearance": ("Follow system", "跟隨系統", "跟随系统", "システムに従う", "시스템 설정", "Systemeinstellung", "Suivre le système", "Seguir el sistema", "Seguir o sistema"),
    "Light": ("Light", "淺色", "浅色", "ライト", "라이트", "Hell", "Clair", "Claro", "Claro"),
    "Dark": ("Dark", "深色", "深色", "ダーク", "다크", "Dunkel", "Sombre", "Oscuro", "Escuro"),
    "Action16": ("Create",) * len(LANGUAGES),
    "Checking components…": (
        "Checking components…", "正在檢查元件…", "正在检查组件…", "コンポーネントを確認中…", "구성 요소 확인 중…",
        "Komponenten werden geprüft…", "Vérification des composants…", "Comprobando componentes…", "Verificando componentes…"),
    "Starting controller services…": (
        "Starting controller services…", "正在啟動手把服務…", "正在启动手柄服务…", "コントローラーサービスを起動中…", "컨트롤러 서비스 시작 중…",
        "Controller-Dienste werden gestartet…", "Démarrage des services de manette…", "Iniciando los servicios del mando…", "Iniciando os serviços do controle…"),
    "Connecting the controller…": (
        "Connecting the controller…", "正在連接手把…", "正在连接手柄…", "コントローラーを接続中…", "컨트롤러 연결 중…",
        "Controller wird verbunden…", "Connexion de la manette…", "Conectando el mando…", "Conectando o controle…"),
    "Controller ready": (
        "Controller ready", "手把已就緒", "手柄已就绪", "コントローラー準備完了", "컨트롤러 준비 완료",
        "Controller bereit", "Manette prête", "Mando listo", "Controle pronto"),
    "Stopped": ("Stopped", "已停止", "已停止", "停止済み", "중지됨", "Gestoppt", "Arrêté", "Detenido", "Parado"),
    "Accessibility: allowed": (
        "Accessibility: allowed", "輔助使用：已允許", "辅助功能：已允许", "アクセシビリティ：許可済み", "손쉬운 사용: 허용됨",
        "Bedienungshilfen: erlaubt", "Accessibilité : autorisée", "Accesibilidad: permitida", "Acessibilidade: permitida"),
    "Accessibility: permission needed": (
        "Accessibility: permission needed", "輔助使用：需要授權", "辅助功能：需要授权", "アクセシビリティ：許可が必要", "손쉬운 사용: 권한 필요",
        "Bedienungshilfen: Zugriff erforderlich", "Accessibilité : autorisation requise", "Accesibilidad: permiso necesario", "Acessibilidade: permissão necessária"),
    "Open Accessibility Settings": (
        "Open Accessibility Settings", "開啟輔助使用設定", "打开辅助功能设置", "アクセシビリティ設定を開く", "손쉬운 사용 설정 열기",
        "Bedienungshilfen öffnen", "Ouvrir les réglages d’accessibilité", "Abrir ajustes de accesibilidad", "Abrir ajustes de acessibilidade"),
    "Show diagnostics": (
        "Show diagnostics", "顯示診斷資訊", "显示诊断信息", "診断情報を表示", "진단 정보 표시",
        "Diagnose anzeigen", "Afficher le diagnostic", "Mostrar diagnóstico", "Mostrar diagnóstico"),
    "Hide diagnostics": (
        "Hide diagnostics", "隱藏診斷資訊", "隐藏诊断信息", "診断情報を隠す", "진단 정보 숨기기",
        "Diagnose ausblenden", "Masquer le diagnostic", "Ocultar diagnóstico", "Ocultar diagnóstico"),
    "Move and navigate": (
        "Move and navigate", "移動與方向操作", "移动与方向操作", "移動と方向操作", "이동 및 방향 조작",
        "Bewegen und navigieren", "Déplacement et navigation", "Movimiento y navegación", "Movimento e navegação"),
    "Face buttons": (
        "Face buttons", "面鍵", "面部按键", "フェイスボタン", "페이스 버튼",
        "Aktionstasten", "Boutons de façade", "Botones frontales", "Botões frontais"),
    "D-pad": ("D-pad", "十字鍵", "十字键", "方向キー", "방향 패드", "Steuerkreuz", "Croix directionnelle", "Cruceta", "Direcional"),
    "Circle: %@ · Options: %@ · Pause / enable: Esc": (
        "Circle: %@ · Options: %@ · Pause / enable: Esc", "圓圈：%@ · Options：%@ · 暫停／啟用：Esc",
        "圆圈：%@ · Options：%@ · 暂停／启用：Esc", "○：%@ · Options：%@ · 一時停止／有効化：Esc",
        "○: %@ · Options: %@ · 일시 정지/켜기: Esc", "Kreis: %@ · Options: %@ · Pause / Aktivieren: Esc",
        "Rond : %@ · Options : %@ · Pause / activer : Esc", "Círculo: %@ · Options: %@ · Pausar / activar: Esc",
        "Círculo: %@ · Options: %@ · Pausar / ativar: Esc"),
    "Circle is %@. Return is Options; Esc pauses or enables keyboard control.": (
        "Circle is %@. Return is Options; Esc pauses or enables keyboard control.",
        "圓圈鍵是 %@。Return 對應 Options；Esc 暫停或啟用鍵盤控制。", "圆圈键是 %@。Return 对应 Options；Esc 暂停或启用键盘控制。",
        "○ は %@ です。Return は Options、Esc はキーボード操作の一時停止／有効化です。",
        "○는 %@입니다. Return은 Options이며 Esc는 키보드 조작을 일시 정지하거나 켭니다.",
        "Kreis ist %@. Return ist Options; Esc pausiert oder aktiviert die Tastatursteuerung.",
        "Rond correspond à %@. Retour correspond à Options ; Esc suspend ou active le clavier.",
        "Círculo es %@. Retorno es Options; Esc pausa o activa el teclado.",
        "Círculo é %@. Return é Options; Esc pausa ou ativa o teclado."),
    "Preparing components…": (
        "Preparing components…", "正在準備元件…", "正在准备组件…", "コンポーネントを準備中…", "구성 요소 준비 중…",
        "Komponenten werden vorbereitet…", "Préparation des composants…", "Preparando componentes…", "Preparando componentes…"),
    "Checking required components…": (
        "Checking required components…", "正在檢查必要元件…", "正在检查所需组件…", "必要なコンポーネントを確認中…", "필수 구성 요소 확인 중…",
        "Erforderliche Komponenten werden geprüft…", "Vérification des composants requis…", "Comprobando componentes necesarios…", "Verificando componentes necessários…"),
    "Downloading the controller component…": (
        "Downloading the controller component…", "正在下載手把元件…", "正在下载手柄组件…", "コントローラーコンポーネントをダウンロード中…", "컨트롤러 구성 요소 다운로드 중…",
        "Controller-Komponente wird heruntergeladen…", "Téléchargement du composant manette…", "Descargando el componente del mando…", "Baixando o componente do controle…"),
    "Downloading VirtualHere…": (
        "Downloading VirtualHere…", "正在下載 VirtualHere…", "正在下载 VirtualHere…", "VirtualHere をダウンロード中…", "VirtualHere 다운로드 중…",
        "VirtualHere wird heruntergeladen…", "Téléchargement de VirtualHere…", "Descargando VirtualHere…", "Baixando o VirtualHere…"),
    "Required components are ready.": (
        "Required components are ready.", "必要元件已就緒。", "所需组件已就绪。", "必要なコンポーネントの準備ができました。", "필수 구성 요소가 준비되었습니다.",
        "Die erforderlichen Komponenten sind bereit.", "Les composants requis sont prêts.", "Los componentes necesarios están listos.", "Os componentes necessários estão prontos."),
    "Stopping…": (
        "Stopping…", "正在停止…", "正在停止…", "停止中…", "중지 중…", "Wird gestoppt…", "Arrêt en cours…", "Deteniendo…", "Parando…"),
    "Ready to connect.": (
        "Ready to connect.", "已準備好連接。", "已准备好连接。", "接続の準備ができました。", "연결할 준비가 되었습니다.",
        "Bereit zum Verbinden.", "Prêt à connecter.", "Listo para conectar.", "Pronto para conectar."),
    "Connect and enable control": (
        "Connect and enable control", "連接並啟用控制", "连接并启用控制", "接続して操作を有効化", "연결하고 조작 켜기",
        "Verbinden und Steuerung aktivieren", "Connecter et activer les commandes", "Conectar y activar controles", "Conectar e ativar comandos"),
    "Pause (Esc)": (
        "Pause (Esc)", "暫停（Esc）", "暂停（Esc）", "一時停止（Esc）", "일시 정지(Esc)",
        "Pausieren (Esc)", "Pause (Esc)", "Pausar (Esc)", "Pausar (Esc)"),
    "Enable (Esc)": (
        "Enable (Esc)", "啟用（Esc）", "启用（Esc）", "有効化（Esc）", "켜기(Esc)",
        "Aktivieren (Esc)", "Activer (Esc)", "Activar (Esc)", "Ativar (Esc)"),
    "Components and licenses": (
        "Components and licenses", "元件與授權", "组件与许可", "コンポーネントとライセンス", "구성 요소 및 라이선스",
        "Komponenten und Lizenzen", "Composants et licences", "Componentes y licencias", "Componentes e licenças"),
    "The Mac version uses VirtualHere and the built-in macOS virtualization framework. Required vendor components are downloaded from VirtualHere when first connecting. No Docker or Parallels is needed.": (
        "The Mac version uses VirtualHere and the built-in macOS virtualization framework. Required vendor components are downloaded from VirtualHere when first connecting. No Docker or Parallels is needed.",
        "Mac 版使用 VirtualHere 與 macOS 內建虛擬化功能。首次連接時會從 VirtualHere 下載所需官方元件，不需要 Docker 或 Parallels。",
        "Mac 版使用 VirtualHere 和 macOS 内置虚拟化功能。首次连接时会从 VirtualHere 下载所需官方组件，无需 Docker 或 Parallels。",
        "Mac 版は VirtualHere と macOS 標準の仮想化機能を使用します。初回接続時に必要な公式コンポーネントを VirtualHere からダウンロードします。Docker や Parallels は不要です。",
        "Mac 버전은 VirtualHere와 macOS 내장 가상화 기능을 사용합니다. 처음 연결할 때 필요한 공식 구성 요소를 VirtualHere에서 다운로드합니다. Docker나 Parallels는 필요하지 않습니다.",
        "Die Mac-Version nutzt VirtualHere und die integrierte Virtualisierung von macOS. Benötigte Herstellerkomponenten werden beim ersten Verbinden von VirtualHere heruntergeladen. Docker oder Parallels sind nicht erforderlich.",
        "La version Mac utilise VirtualHere et la virtualisation intégrée à macOS. Les composants officiels requis sont téléchargés depuis VirtualHere lors de la première connexion. Docker et Parallels ne sont pas nécessaires.",
        "La versión para Mac usa VirtualHere y la virtualización integrada en macOS. Los componentes oficiales necesarios se descargan de VirtualHere al conectar por primera vez. No se necesita Docker ni Parallels.",
        "A versão para Mac usa o VirtualHere e a virtualização integrada ao macOS. Os componentes oficiais necessários são baixados do VirtualHere na primeira conexão. Não é necessário Docker nem Parallels."),
    "VirtualHere client license": (
        "VirtualHere client license", "VirtualHere 用戶端授權", "VirtualHere 客户端许可", "VirtualHere クライアントのライセンス", "VirtualHere 클라이언트 라이선스",
        "VirtualHere-Client-Lizenz", "Licence du client VirtualHere", "Licencia del cliente VirtualHere", "Licença do cliente VirtualHere"),
    "VirtualHere server license": (
        "VirtualHere server license", "VirtualHere 伺服器授權", "VirtualHere 服务器许可", "VirtualHere サーバーのライセンス", "VirtualHere 서버 라이선스",
        "VirtualHere-Server-Lizenz", "Licence du serveur VirtualHere", "Licencia del servidor VirtualHere", "Licença do servidor VirtualHere"),
    "Open source license": (
        "Open source license", "開源授權", "开源许可", "オープンソースライセンス", "오픈 소스 라이선스",
        "Open-Source-Lizenz", "Licence open source", "Licencia de código abierto", "Licença de código aberto"),
    "Close": ("Close", "關閉", "关闭", "閉じる", "닫기", "Schließen", "Fermer", "Cerrar", "Fechar"),
    "Allow Accessibility for this app, then connect again.": (
        "Allow Accessibility for this app, then connect again.", "請允許此 App 的輔助使用權限，再重新連接。", "请允许此 App 的辅助功能权限，然后重新连接。",
        "このアプリのアクセシビリティを許可してから再接続してください。", "이 앱의 손쉬운 사용 권한을 허용한 뒤 다시 연결하세요.",
        "Erlaube dieser App den Zugriff auf Bedienungshilfen und verbinde sie erneut.", "Autorisez l’accessibilité pour cette app, puis reconnectez-la.",
        "Permite Accesibilidad para esta app y vuelve a conectar.", "Permita a Acessibilidade para este app e conecte novamente."),
    "Secure keyboard input is active. Leave the password field and try again.": (
        "Secure keyboard input is active. Leave the password field and try again.", "安全鍵盤輸入已啟用。請離開密碼欄位後重試。", "安全键盘输入已启用。请离开密码输入框后重试。",
        "安全なキーボード入力が有効です。パスワード欄を離れてから再試行してください。", "보안 키보드 입력이 활성화되어 있습니다. 암호 입력란에서 나온 뒤 다시 시도하세요.",
        "Die sichere Tastatureingabe ist aktiv. Verlasse das Passwortfeld und versuche es erneut.", "La saisie sécurisée est active. Quittez le champ de mot de passe et réessayez.",
        "La entrada segura del teclado está activa. Sal del campo de contraseña e inténtalo de nuevo.", "A entrada segura do teclado está ativa. Saia do campo de senha e tente novamente."),
    "Could not start controller output. Please retry. Details: %@": (
        "Could not start controller output. Please retry. Details: %@", "無法啟動手把輸出，請重試。詳細資訊：%@", "无法启动手柄输出，请重试。详细信息：%@",
        "コントローラー出力を開始できませんでした。再試行してください。詳細：%@", "컨트롤러 출력을 시작하지 못했습니다. 다시 시도하세요. 세부 정보: %@",
        "Controller-Ausgabe konnte nicht gestartet werden. Versuche es erneut. Details: %@", "Impossible de démarrer la sortie manette. Réessayez. Détails : %@",
        "No se pudo iniciar la salida del mando. Inténtalo de nuevo. Detalles: %@", "Não foi possível iniciar a saída do controle. Tente novamente. Detalhes: %@"),
    "The controller connection stopped. Details: %@": (
        "The controller connection stopped. Details: %@", "手把連線已停止。詳細資訊：%@", "手柄连接已停止。详细信息：%@",
        "コントローラー接続が停止しました。詳細：%@", "컨트롤러 연결이 중단되었습니다. 세부 정보: %@",
        "Die Controller-Verbindung wurde beendet. Details: %@", "La connexion de la manette s’est arrêtée. Détails : %@",
        "La conexión del mando se ha detenido. Detalles: %@", "A conexão do controle foi interrompida. Detalhes: %@"),
    "Received: %@ · Accepted: %@ · Output: %@": (
        "Received: %@ · Accepted: %@ · Output: %@", "收到：%@ · 接受：%@ · 輸出：%@", "收到：%@ · 接受：%@ · 输出：%@",
        "受信：%@ · 受付：%@ · 出力：%@", "수신: %@ · 허용: %@ · 출력: %@", "Empfangen: %@ · Akzeptiert: %@ · Ausgabe: %@",
        "Reçus : %@ · Acceptés : %@ · Sortie : %@", "Recibidos: %@ · Aceptados: %@ · Salida: %@", "Recebidos: %@ · Aceitos: %@ · Saída: %@"),
    "Quit %@": ("Quit %@", "結束 %@", "退出 %@", "%@ を終了", "%@ 종료", "%@ beenden", "Quitter %@", "Salir de %@", "Sair de %@"),
    "About %@": ("About %@", "關於 %@", "关于 %@", "%@ について", "%@ 정보", "Über %@", "À propos de %@", "Acerca de %@", "Sobre %@"),
    "All features are free. Donations are optional.": (
        "All features are free. Donations are optional.", "所有功能免費，贊助完全自願。", "所有功能免费，赞助完全自愿。",
        "すべての機能は無料です。寄付は任意です。", "모든 기능은 무료이며 후원은 선택 사항입니다.",
        "Alle Funktionen sind kostenlos. Spenden sind freiwillig.", "Toutes les fonctions sont gratuites. Les dons sont facultatifs.",
        "Todas las funciones son gratuitas. Las donaciones son opcionales.", "Todos os recursos são gratuitos. As doações são opcionais."),
    "Mac %@ · Apple Silicon · macOS 15+": (
        "Mac %@ · Apple Silicon · macOS 15+", "Mac %@ · Apple Silicon · macOS 15 以上", "Mac %@ · Apple Silicon · macOS 15 及以上",
        "Mac %@ · Apple Silicon · macOS 15 以降", "Mac %@ · Apple Silicon · macOS 15 이상", "Mac %@ · Apple Silicon · macOS 15+",
        "Mac %@ · Apple Silicon · macOS 15+", "Mac %@ · Apple Silicon · macOS 15+", "Mac %@ · Apple Silicon · macOS 15+"),
    "Feedback link is unavailable.": (
        "Feedback link is unavailable.", "目前無法使用回饋連結。", "目前无法使用反馈链接。", "フィードバック用リンクを利用できません。", "의견 보내기 링크를 사용할 수 없습니다.",
        "Der Feedback-Link ist nicht verfügbar.", "Le lien de retour est indisponible.", "El enlace de comentarios no está disponible.", "O link de comentários não está disponível."),
    "Restore selected action": (
        "Restore selected action", "還原所選動作", "还原所选动作", "選択した操作を初期化", "선택한 동작 초기화",
        "Ausgewählte Aktion zurücksetzen", "Rétablir l’action sélectionnée", "Restablecer la acción seleccionada", "Restaurar a ação selecionada"),
    "Enter an interval from 20 to 5000 milliseconds.": (
        "Enter an interval from 20 to 5000 milliseconds.", "請輸入 20 到 5000 毫秒的間隔。", "请输入 20 到 5000 毫秒的间隔。",
        "20～5000 ミリ秒の間隔を入力してください。", "20~5000밀리초 사이의 간격을 입력하세요.",
        "Gib ein Intervall von 20 bis 5000 Millisekunden ein.", "Saisissez un intervalle de 20 à 5000 millisecondes.",
        "Introduce un intervalo de 20 a 5000 milisegundos.", "Insira um intervalo de 20 a 5000 milissegundos."),
    "Changes are saved only when you choose Save.": (
        "Changes are saved only when you choose Save.", "只有按下「儲存」才會保存變更。", "只有点击“保存”才会保存更改。",
        "「保存」を選ぶまで変更は保存されません。", "저장을 선택해야 변경 사항이 저장됩니다.",
        "Änderungen werden erst mit „Speichern“ gespeichert.", "Les modifications ne sont enregistrées qu’avec « Enregistrer ».",
        "Los cambios solo se guardan al elegir Guardar.", "As alterações só são salvas ao escolher Salvar."),
    "Return (Enter)": ("Return (Enter)", "Return（Enter）", "Return（Enter）", "Return（Enter）", "Return(Enter)",
                       "Return (Eingabe)", "Retour (Entrée)", "Retorno (Intro)", "Return (Enter)"),
    "MacRuntimeFailure": (
        "Could not prepare controller output. Check the details and try again.", "無法準備手把輸出。請查看詳細資訊後重試。",
        "无法准备手柄输出。请查看详细信息后重试。", "コントローラー出力を準備できませんでした。詳細を確認して再試行してください。",
        "컨트롤러 출력을 준비하지 못했습니다. 세부 정보를 확인한 뒤 다시 시도하세요.",
        "Die Controller-Ausgabe konnte nicht vorbereitet werden. Prüfe die Details und versuche es erneut.",
        "Impossible de préparer la sortie manette. Consultez les détails et réessayez.",
        "No se pudo preparar la salida del mando. Revisa los detalles e inténtalo de nuevo.",
        "Não foi possível preparar a saída do controle. Confira os detalhes e tente novamente."),
    "MacPermissionRequired": (
        "Allow Accessibility access for this app in System Settings, then try again.", "請在系統設定允許此 App 的輔助使用權限，再重試。",
        "请在系统设置中允许此 App 的辅助功能权限，然后重试。", "システム設定でこのアプリのアクセシビリティを許可してから再試行してください。",
        "시스템 설정에서 이 앱의 손쉬운 사용 권한을 허용한 뒤 다시 시도하세요.",
        "Erlaube dieser App unter Systemeinstellungen den Zugriff auf Bedienungshilfen und versuche es erneut.",
        "Autorisez l’accès de cette app à l’accessibilité dans Réglages Système, puis réessayez.",
        "Permite el acceso de esta app a Accesibilidad en Ajustes del Sistema e inténtalo de nuevo.",
        "Permita o acesso deste app à Acessibilidade nos Ajustes do Sistema e tente novamente."),
    "MacUsage": (
        "Open the app, prepare the controller, then enable keyboard control. Esc pauses or enables control in Remote Play.",
        "開啟 App 並準備手把後，啟用鍵盤控制。在 Remote Play 中按 Esc 暫停或啟用。",
        "打开 App 并准备手柄后，启用键盘控制。在 Remote Play 中按 Esc 暂停或启用。",
        "アプリでコントローラーを準備して、キーボード操作を有効にします。Remote Play では Esc で一時停止／有効化できます。",
        "앱에서 컨트롤러를 준비한 뒤 키보드 조작을 켜세요. Remote Play에서 Esc로 일시 정지하거나 켤 수 있습니다.",
        "Öffne die App, bereite den Controller vor und aktiviere die Tastatursteuerung. Esc pausiert oder aktiviert sie in Remote Play.",
        "Ouvrez l’app, préparez la manette, puis activez le clavier. Esc suspend ou active les commandes dans Remote Play.",
        "Abre la app, prepara el mando y activa el teclado. Esc pausa o activa los controles en Remote Play.",
        "Abra o app, prepare o controle e ative o teclado. Esc pausa ou ativa os comandos no Remote Play."),
    "MacStopMessage": (
        "This stops keyboard control and disconnects this app’s controller output. Pause your game first.",
        "這會停止鍵盤控制並中斷本 App 的手把輸出。請先暫停遊戲。", "这会停止键盘控制并断开本 App 的手柄输出。请先暂停游戏。",
        "キーボード操作とこのアプリのコントローラー出力を停止します。先にゲームを一時停止してください。",
        "키보드 조작과 이 앱의 컨트롤러 출력을 중단합니다. 먼저 게임을 일시 정지하세요.",
        "Dies stoppt die Tastatursteuerung und trennt die Controller-Ausgabe dieser App. Pausiere zuerst das Spiel.",
        "Cela arrête le clavier et déconnecte la sortie manette de cette app. Mettez d’abord le jeu en pause.",
        "Esto detiene el teclado y desconecta la salida del mando de esta app. Pausa primero el juego.",
        "Isso interrompe o teclado e desconecta a saída do controle deste app. Pause o jogo primeiro."),
}

# Older shared catalogue keys that describe a Windows driver or physical relay
# must remain callable without exposing those instructions in the Mac product.
MAC_KEY_REDIRECTS = {
    "Usage": "MacUsage", "InvalidArguments": "MacUsage",
    "StopConfirmMessage": "MacStopMessage",
    "KeyboardSetupFailure": "MacPermissionRequired",
    "FailureKeyboardSetup": "MacPermissionRequired",
    "RelayStartupFailure": "MacRuntimeFailure",
    "FailureUsbipMissing": "MacRuntimeFailure", "FailureUsbipInvalid": "MacRuntimeFailure",
    "FailureExistingImport": "MacRuntimeFailure", "FailureBackendInvalid": "MacRuntimeFailure",
    "FailurePortsUnavailable": "MacRuntimeFailure", "FailureUsbAttach": "MacRuntimeFailure",
    "FailureBackendStart": "MacRuntimeFailure", "FailureUsbipTimeout": "MacRuntimeFailure",
    "FailureUsbipOutput": "MacRuntimeFailure",
}


def split_expressions(source: str, separator: str) -> list[str]:
    """Split outside regular C# string literals, refusing unsupported syntax."""
    parts, start, quoted, escaped = [], 0, False, False
    for index, char in enumerate(source):
        if quoted:
            if escaped: escaped = False
            elif char == "\\": escaped = True
            elif char == '"': quoted = False
        elif char == '"': quoted = True
        elif char == separator:
            parts.append(source[start:index].strip()); start = index + 1
    if quoted: raise ValueError("Unterminated C# string")
    parts.append(source[start:].strip())
    return parts


def string_expression(source: str, constants: dict[str, str]) -> str:
    result = []
    for piece in split_expressions(source, "+"):
        if re.fullmatch(r'"(?:\\.|[^"\\])*"', piece, re.DOTALL):
            result.append(json.loads(piece))
        elif piece in constants:
            result.append(constants[piece])
        else:
            raise ValueError(f"Unsupported C# string expression: {piece!r}")
    return "".join(result)


def extract_rows(source: str) -> dict[str, tuple[str, ...]]:
    constants = {name: json.loads(value) for name, value in re.findall(
        r'const string\s+(\w+)\s*=\s*("(?:\\.|[^"\\])*")\s*;', source)}
    rows = {}
    for match in re.finditer(r'\bAdd\(tables,\s*', source):
        quoted, escaped, end = False, False, match.end()
        while end < len(source):
            char = source[end]
            if quoted:
                if escaped: escaped = False
                elif char == "\\": escaped = True
                elif char == '"': quoted = False
            elif char == '"': quoted = True
            elif char == ")": break
            end += 1
        if end == len(source): raise ValueError("Unterminated Add(tables, ...) call")
        parts = split_expressions(source[match.end():end], ",")
        values = tuple(string_expression(part, constants) for part in parts)
        if len(values) != 1 + len(LANGUAGES): raise ValueError(f"Incomplete row: {values[0]}")
        key, *translations = values
        if key in rows: raise ValueError(f"Duplicate key: {key}")
        rows[key] = tuple(translations)
    if not rows: raise ValueError("No translation rows found")
    return rows


def mac_copy(value: str) -> str:
    return value.replace("\r\n", "\n").replace("F8", "Esc").replace("Windows", "macOS")


def format_copy(value: str) -> str:
    # printf placeholders are explicit positional objects; escaping all other
    # percent signs prevents a translated literal from becoming a format token.
    if "%@" in value:
        if re.search(r"\{\d+\}", value): raise ValueError("Mixed format styles")
        pieces = value.split("%@")
        return "".join(piece.replace("%", "%%") + (f"%{index + 1}$@" if index + 1 < len(pieces) else "")
                       for index, piece in enumerate(pieces))
    if not re.search(r"\{\d+\}", value): return value
    value = value.replace("%", "%%")
    return re.sub(r"\{(\d+)\}", lambda m: f"%{int(m[1]) + 1}$@", value)


def build_catalogue() -> dict:
    rows, aliases, source_hashes = {}, {}, {}
    for relative in SOURCES:
        data = (ROOT / relative).read_bytes()
        source_hashes[relative] = hashlib.sha256(data).hexdigest()
        for key, values in extract_rows(data.decode("utf-8-sig")).items():
            rows[key] = tuple(mac_copy(value) for value in values)
            # Both old English literals and their Mac equivalents are aliases.
            aliases[values[0]] = key
            aliases[mac_copy(values[0])] = key
    rows.update(MAC_ROWS)
    rows = {key: tuple(value.replace("PS Remote Play Keyboard", "Three Cats Keybridge")
                       for value in values) for key, values in rows.items()}
    for key, target in MAC_KEY_REDIRECTS.items(): rows[key] = rows[target]
    # Stable historical aliases remain accepted after presentation-only renaming.
    aliases["DualSense"] = "ProductExperimental"
    aliases["Creating the DualSense controller…"] = "Creating the DualSense controller…"
    aliases["PS Remote Play Keyboard V %1$@ feedback"] = "FeedbackSubjectFmt"
    for key, values in rows.items():
        if len(values) != len(LANGUAGES) or any(not value.strip() for value in values):
            raise ValueError(f"Incomplete Mac translations: {key}")
        expected = sorted(re.findall(r"\{\d+\}", values[0]))
        for value in values:
            if sorted(re.findall(r"\{\d+\}", value)) != expected or value.count("%@") != values[0].count("%@"):
                raise ValueError(f"Mismatched format arguments: {key}")
        aliases[values[0]] = key
        aliases[format_copy(values[0])] = key
    return {
        "schemaVersion": 1,
        "languages": list(LANGUAGES),
        "sourceSHA256": source_hashes,
        "aliases": dict(sorted(aliases.items())),
        "strings": {language: {key: format_copy(values[index]) for key, values in sorted(rows.items())}
                    for index, language in enumerate(LANGUAGES)},
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    catalogue = build_catalogue()
    encoded = (json.dumps(catalogue, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    if args.check:
        if not args.output.is_file() or args.output.read_bytes() != encoded:
            raise SystemExit("Mac strings are out of date; run packaging/macos/build_strings.py")
    else:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_bytes(encoded)
    print(f"Mac strings: {len(catalogue['strings']['en'])} keys, {len(LANGUAGES)} languages")


if __name__ == "__main__": main()
