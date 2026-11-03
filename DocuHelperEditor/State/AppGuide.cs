using DocuHelper.Agents;
using DocuHelperEditor.State.Ribbon;

namespace DocuHelperEditor.State
{
    public static class AppGuide
    {
        public const string ScreenTopic = "app";

        private const string ScreenText = """
            How the app's screen works
            The header runs across the top, the 작업 폴더 panel with the file list is on the left, the open files are tabs in the middle (each with its ribbon, page and status bar), and the 도우미 panel is on the right. F6 and Shift+F6 move between the file list, the document and the 도우미 panel. The menus pages of the guide list each kind of file's ribbon.

            Header (top right)
            - The save state reads 저장 안 한 파일 N개, 모두 저장했어요 or 바뀐 것 없음.
            - 모두 저장 saves every unsaved file, the same as Ctrl+S. 기록 opens the 저장 기록 panel and 설정 opens the settings.
            - A 이번 달 N% pill shows only when a monthly limit is set and nearly used; a 새 버전 pill shows when an update is ready.

            Folders and files
            - The start screen asks 어떤 작업 폴더를 열까요?: pick a folder under 최근에 연 작업 폴더, use 새 작업 폴더 만들기 (type a name, then 만들고 열기) or 이미 있는 폴더 열기 → 폴더 고르기. The 다른 작업 폴더 열기 icon above the file list goes back there; it refuses while the assistant works or a file is unsaved.
            - The app keeps saved versions and conversations in a hidden .문서도우미 folder inside the working folder.
            - Click a file in the list to open it as a tab. Right-click (or Shift+F10) a file for 탐색기에서 보기, 다른 곳에 저장하기 (exports a copy), 이름 바꾸기 (also F2) and 작업 폴더에서 빼기 (moves it to the recycle bin). The words under a name tell its state, such as 도우미가 고치는 중, 도우미가 N곳 고침, 저장 전 or 방금 저장함.
            - 새 파일 at the bottom left opens 새 파일 만들기 with five kinds: 엑셀 (표와 계산), 워드 (글과 보고서), 한글 (공문과 안내문), 파워포인트 (발표 자료) and 대시보드 (바꿔 보는 한 장). Type a name, then 만들기. New 한글 files are .hwpx unless the settings say .hwp.
            - 파일 넣기 at the bottom right, or dragging files onto the window, copies Excel, Word, 한글, PowerPoint and dashboard files into the working folder; the originals stay where they were and a taken name becomes 이름 (2). Pictures, PDFs and text files dragged onto the window are attached to the next message to the assistant instead. PDFs cannot be edited.
            - The 파일 목록 접기 icon folds the file list into a thin strip.

            Open files
            - Open files are tabs above the document, and one document shows at a time. The x on a tab closes it, and the file stays in the list; with unsaved changes the app asks 저장하고 닫기, 저장 안 하고 닫기 or 취소.
            - Zoom with Ctrl and the mouse wheel or the slider at the bottom right (50 to 200%). Ctrl+F1 folds the ribbon (도구 상자 in 한글).

            Saving and earlier versions
            - Ctrl+S saves every unsaved file; 파일 → 저장 saves only the file on screen. 다른 이름으로 저장 exports a copy somewhere else and leaves the working file as it is. Older .xls, .doc and .ppt files are saved as new .xlsx, .docx and .pptx files next to the original.
            - 기록 keeps the file as it was when it was first added and at every save. 기록 in the header, or 기록 보기 on the 저장했어요 notice, lists those versions of the file on screen, newest first, and 이때로 되돌리기 brings one back; going back is recorded too, so it can be reversed the same way.
            - Saving removes the highlights of the assistant's changes.
            - PDF로 저장 and 인쇄 are in the 파일 menu of each kind of file. Printing is not ready for every kind yet; the button then says 아직 준비 중이에요, and PDF로 저장 is the way around it. 파일 → Word에서 열기, Excel에서 열기, PowerPoint에서 열기 or 한글에서 열기 opens the saved file in that program.

            Undo
            - Ctrl+Z undoes and Ctrl+Y or Ctrl+Shift+Z redoes, outside text boxes; the 실행 취소 and 다시 실행 arrows sit at the right end of the ribbon (in 한글, 되돌리기 and 다시 실행 start the second toolbar row).
            - One undo list covers the whole folder, up to 30 steps, and a whole assistant task is one step.

            The 도우미 panel
            - Type a request and press Enter or 보내기 (Shift+Enter starts a new line). The chip above the box shows the file and selection that go with the request. Suggested requests fill the box without sending it.
            - Attach pictures, PDFs and text files with the clip button 파일 더하기, by dragging them onto the window, or by pasting a picture with Ctrl+V: up to 10 per message, pictures up to 30 MB, PDFs up to 10 MB and text files up to 200 KB.
            - Below the box are the model button (such as Sonnet 5.5) and 노력 (빠르게, 보통, 꼼꼼하게, 최대한); a change applies from the next request.
            - The 대시보드 button next to the clip (not shown while a dashboard is open) makes a dashboard from the conversation: pick a shape (한 장 요약, 그래프 위주, 카드로 보기, 표로 보기), check 읽을 엑셀 (바꾸기 changes it), optionally add 바꿔 볼 것 (기간, 목표, 고르기), then press 대시보드 만들기. The app makes the file from that shape and the assistant fills it, reading the Excel files without changing them. Esc turns the button off.
            - There is no voice input. Word can read a document aloud with 검토 → 소리내어 읽기.
            - While the assistant works, its card lists the steps with 멈추기, and stopping puts the files it was changing back as they were. The file it is writing is locked under a dark banner 도우미가 이 파일을 고치는 중이에요; saving, undo, renaming, going back in 기록 and opening another folder wait until it finishes, but other files can be looked at.
            - A question from the assistant opens over the panel (도우미가 여쭤봐요): pick an answer or type one.
            - When it finishes, the result card lists each file with 바뀐 곳 N, and a click on a line jumps to that place. 이대로 저장하기 saves and 더 고칠래요 asks for more. The changes stay highlighted until the file is saved.
            - To check or take back the assistant's changes, 바뀐 곳 N / M with 이전 바뀐 곳 and 다음 바뀐 곳 steps through them and 전부 되돌리기 removes everything the assistant changed in that file since the last save. They are on the status bar in Word and 한글, and on the strip under the document in Excel, PowerPoint and dashboards. Ctrl+Z also takes back a whole task.
            - The 새 대화 icon starts over, the 지난 대화 icon lists the last 20 conversations in this folder and reopens one to continue, and the 도우미 칸 접기 icon folds the panel.

            Settings (설정 in the header)
            - 도우미 열쇠: a Claude or a ChatGPT key, one is enough, with 연결 확인. Keys are typed only here, never in the conversation.
            - 도우미: 기본 모델, 기본 노력, 큰 변경은 먼저 물어보기 (on at first) and 워드 변경 내용 추적으로도 남기기.
            - 사용량: what this month cost, 한 달 한도, when to warn and what happens at the limit.
            - 작업 폴더: where new working folders go and whether the last folder opens at start.
            - 한글 파일: whether edited and new 한글 files are saved as .hwp or .hwpx.
            - 화면: 글자 크기 for the menus and the conversation. 업데이트 and 정보 complete the list.
            """;

        public static void AddTo(AgentGuide guide)
        {
            guide.Add(new AgentGuidePage
            {
                Topic = ScreenTopic,
                Title = "앱 사용법",
                Summary = "the app's screen outside the ribbons: folders and the file list, new files, adding files, tabs, saving, 기록 (earlier versions), undo, the 도우미 panel (attachments, models, stopping, questions, checking and taking back changes, earlier conversations), settings and shortcuts",
                Text = ScreenText.Replace("\r\n", "\n"),
            });

            foreach (var page in RibbonGuide.Pages())
                guide.Add(page);
        }
    }
}
