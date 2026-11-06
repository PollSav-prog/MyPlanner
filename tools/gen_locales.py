#!/usr/bin/env python3
"""Генерирует Locales/*.json из единой таблицы и проверяет полноту переводов."""
import json, os, re, glob, sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
OUT = os.path.join(ROOT, "src", "MyPlanner", "Locales")
SRC = os.path.join(ROOT, "src", "MyPlanner")

META = {
    "ru-RU": ("Русский", "RU"),
    "en-US": ("English", "EN"),
    "de-DE": ("Deutsch", "DE"),
    "fr-FR": ("Français", "FR"),
}

# key: (ru, en, de, fr)
T = {
# --- общие ---
"app_title": ("Мой планировщик", "My Planner", "Mein Planer", "Mon planificateur"),
"plans_title": ("МОИ ПЛАНЫ", "MY PLANS", "MEINE PLÄNE", "MES PLANS"),
"no_tasks": ("На этот день задач нет.\nНажмите «Добавить задачу» или микрофон.", "No tasks for this day.\nPress “Add task” or the microphone.", "Keine Aufgaben für diesen Tag.\nDrücken Sie „Aufgabe hinzufügen“ oder das Mikrofon.", "Aucune tâche pour ce jour.\nAppuyez sur « Ajouter une tâche » ou sur le micro."),
"btn_today": ("Сегодня", "Today", "Heute", "Aujourd’hui"),
"btn_add_task": ("Добавить задачу", "Add task", "Aufgabe hinzufügen", "Ajouter une tâche"),
"btn_voice": ("Голосом", "By voice", "Per Sprache", "À la voix"),
"btn_ok": ("OK", "OK", "OK", "OK"),
"btn_yes": ("Да", "Yes", "Ja", "Oui"),
"btn_no": ("Нет", "No", "Nein", "Non"),
"btn_cancel": ("Отмена", "Cancel", "Abbrechen", "Annuler"),
"btn_save": ("Сохранить", "Save", "Speichern", "Enregistrer"),
"btn_close": ("Закрыть", "Close", "Schließen", "Fermer"),
"btn_delete": ("Удалить", "Delete", "Löschen", "Supprimer"),
"btn_done": ("Выполнено", "Done", "Erledigt", "Terminé"),
"btn_fix": ("Исправить", "Edit", "Korrigieren", "Corriger"),
"btn_postpone": ("Отложить", "Postpone", "Verschieben", "Reporter"),
"btn_view_task": ("Посмотреть задачу", "View task", "Aufgabe ansehen", "Voir la tâche"),
"btn_snooze_10": ("Отложить на 10 мин", "Snooze 10 min", "10 Min. später", "Reporter de 10 min"),
"btn_snooze_30": ("На 30 мин", "30 min", "30 Min.", "30 min"),
"btn_snooze_60": ("На 60 мин", "60 min", "60 Min.", "60 min"),
"btn_restore_task": ("Восстановить", "Restore", "Wiederherstellen", "Restaurer"),
"btn_delete_forever": ("Удалить навсегда", "Delete forever", "Endgültig löschen", "Supprimer définitivement"),
"btn_clear_history": ("Очистить список", "Clear list", "Liste leeren", "Vider la liste"),
"btn_mic_start": ("Нажмите и говорите", "Press and speak", "Drücken und sprechen", "Appuyez et parlez"),
"btn_recognize_text": ("Разобрать текст", "Analyze text", "Text auswerten", "Analyser le texte"),
"btn_test_voice": ("Проверить голос", "Test voice", "Stimme testen", "Tester la voix"),
"btn_test_sound": ("Проверить сигнал", "Test sound", "Signal testen", "Tester le son"),
"btn_go_sound": ("Настройки звука…", "Sound settings…", "Toneinstellungen …", "Réglages du son…"),
"btn_reset_text": ("Вернуть текст по умолчанию", "Restore default text", "Standardtext wiederherstellen", "Rétablir le texte par défaut"),
"btn_refresh_weather": ("Обновить погоду", "Refresh weather", "Wetter aktualisieren", "Actualiser la météo"),
"btn_backup_now": ("Создать копию сейчас", "Back up now", "Jetzt sichern", "Sauvegarder maintenant"),
"btn_open_backups": ("Открыть папку копий", "Open backups folder", "Sicherungsordner öffnen", "Ouvrir le dossier des sauvegardes"),
"btn_restore": ("Восстановить из копии…", "Restore from backup…", "Aus Sicherung wiederherstellen …", "Restaurer une sauvegarde…"),
"btn_open_data": ("Открыть папку данных", "Open data folder", "Datenordner öffnen", "Ouvrir le dossier des données"),
# --- подсказки правой панели ---
"tip_add": ("Добавить задачу", "Add task", "Aufgabe hinzufügen", "Ajouter une tâche"),
"tip_edit": ("Редактировать задачу", "Edit task", "Aufgabe bearbeiten", "Modifier la tâche"),
"tip_done": ("Выполнено", "Mark as done", "Erledigt", "Terminé"),
"tip_repeat": ("Повторение", "Repeat", "Wiederholung", "Répétition"),
"tip_sound": ("Настройка звука", "Sound settings", "Toneinstellungen", "Réglages du son"),
"tip_quiet": ("Тихий режим", "Quiet mode", "Ruhemodus", "Mode silencieux"),
"tip_weather": ("Погода", "Weather", "Wetter", "Météo"),
"tip_history": ("История", "History", "Verlauf", "Historique"),
"tip_language": ("Язык интерфейса", "Interface language", "Sprache der Oberfläche", "Langue de l’interface"),
"tip_settings": ("Настройки", "Settings", "Einstellungen", "Paramètres"),
"tip_voice": ("Создать задачу голосом", "Create a task by voice", "Aufgabe per Sprache erstellen", "Créer une tâche à la voix"),
"tip_droplet": ("Скоро событие — нажмите, чтобы посмотреть", "Upcoming event — click to view", "Bald ein Termin – zum Ansehen klicken", "Événement proche — cliquez pour voir"),
"move_today": ("Перенести на сегодня", "Move to today", "Auf heute verschieben", "Déplacer à aujourd’hui"),
"move_tomorrow": ("Перенести на завтра", "Move to tomorrow", "Auf morgen verschieben", "Déplacer à demain"),
"move_week": ("Перенести на неделю", "Move by one week", "Um eine Woche verschieben", "Déplacer d’une semaine"),
"status_overdue": ("Просрочено", "Overdue", "Überfällig", "En retard"),
# --- диалог задачи ---
"dlg_new_task": ("Новая задача", "New task", "Neue Aufgabe", "Nouvelle tâche"),
"dlg_edit_task": ("Редактирование задачи", "Edit task", "Aufgabe bearbeiten", "Modifier la tâche"),
"lbl_title": ("Название", "Title", "Titel", "Titre"),
"lbl_description": ("Описание", "Description", "Beschreibung", "Description"),
"lbl_date_time": ("Дата и время", "Date and time", "Datum und Uhrzeit", "Date et heure"),
"lbl_priority": ("Приоритет", "Priority", "Priorität", "Priorité"),
"lbl_category": ("Категория", "Category", "Kategorie", "Catégorie"),
"lbl_remind": ("Напоминать об этой задаче", "Remind me about this task", "An diese Aufgabe erinnern", "Me rappeler cette tâche"),
"lbl_warn": ("Предупреждение-капелька", "Early warning (droplet)", "Vorwarnung (Tropfen)", "Alerte préalable (goutte)"),
"lbl_repeat": ("Повторение", "Repeat", "Wiederholung", "Répétition"),
"prio_low": ("Низкий", "Low", "Niedrig", "Basse"),
"prio_normal": ("Обычный", "Normal", "Normal", "Normale"),
"prio_high": ("Высокий", "High", "Hoch", "Haute"),
"cat_personal": ("Личное", "Personal", "Privat", "Personnel"),
"cat_work": ("Работа", "Work", "Arbeit", "Travail"),
"cat_health": ("Здоровье", "Health", "Gesundheit", "Santé"),
"cat_home": ("Дом", "Home", "Zuhause", "Maison"),
"cat_study": ("Учёба", "Study", "Studium", "Études"),
"warn_default": ("Как в настройках", "As in settings", "Wie in den Einstellungen", "Comme dans les paramètres"),
"warn_off": ("Не предупреждать", "No warning", "Keine Vorwarnung", "Pas d’alerte"),
"warn_30": ("За 30 минут", "30 minutes before", "30 Minuten vorher", "30 minutes avant"),
"warn_1h": ("За 1 час", "1 hour before", "1 Stunde vorher", "1 heure avant"),
"warn_1h30": ("За 1,5 часа", "1.5 hours before", "1,5 Stunden vorher", "1 h 30 avant"),
"warn_2h": ("За 2 часа", "2 hours before", "2 Stunden vorher", "2 heures avant"),
"warn_3h": ("За 3 часа", "3 hours before", "3 Stunden vorher", "3 heures avant"),
"warn_custom": ("Свой интервал (в минутах)", "Custom interval (minutes)", "Eigenes Intervall (Minuten)", "Intervalle personnalisé (minutes)"),
"err_title_empty": ("Введите название задачи.", "Please enter a task title.", "Bitte geben Sie einen Titel ein.", "Veuillez saisir un titre."),
"err_date": ("Выберите дату.", "Please choose a date.", "Bitte wählen Sie ein Datum.", "Veuillez choisir une date."),
"err_time": ("Введите время в формате ЧЧ:ММ, например 15:30.", "Enter the time as HH:mm, for example 15:30.", "Geben Sie die Uhrzeit als HH:mm ein, z. B. 15:30.", "Saisissez l’heure au format HH:mm, par exemple 15:30."),
"err_pick_weekday": ("Выберите хотя бы один день недели.", "Select at least one weekday.", "Wählen Sie mindestens einen Wochentag.", "Sélectionnez au moins un jour de la semaine."),
"err_interval": ("Укажите число дней от 1 до 365.", "Enter a number of days from 1 to 365.", "Geben Sie eine Anzahl von Tagen zwischen 1 und 365 ein.", "Saisissez un nombre de jours entre 1 et 365."),
"msg_select_task": ("Сначала выберите задачу в списке.", "Please select a task in the list first.", "Wählen Sie zuerst eine Aufgabe in der Liste aus.", "Sélectionnez d’abord une tâche dans la liste."),
"msg_confirm_delete": ("Удалить задачу «{0}»? Она останется в истории.", "Delete the task “{0}”? It will stay in the history.", "Aufgabe „{0}“ löschen? Sie bleibt im Verlauf erhalten.", "Supprimer la tâche « {0} » ? Elle restera dans l’historique."),
"err_unexpected": ("Произошла непредвиденная ошибка. Подробности записаны в журнал. Данные сохранены.", "An unexpected error occurred. Details were written to the log. Your data is safe.", "Ein unerwarteter Fehler ist aufgetreten. Details stehen im Protokoll. Ihre Daten sind gesichert.", "Une erreur inattendue s’est produite. Les détails figurent dans le journal. Vos données sont conservées."),
# --- повторение ---
"rep_title": ("Повторение задачи", "Task repeat", "Aufgabenwiederholung", "Répétition de la tâche"),
"rep_none": ("Не повторять", "Do not repeat", "Nicht wiederholen", "Ne pas répéter"),
"rep_daily": ("Каждый день", "Every day", "Jeden Tag", "Chaque jour"),
"rep_weekdays": ("По выбранным дням недели", "On selected weekdays", "An bestimmten Wochentagen", "Certains jours de la semaine"),
"rep_weekly": ("Каждую неделю", "Every week", "Jede Woche", "Chaque semaine"),
"rep_monthly": ("Каждый месяц", "Every month", "Jeden Monat", "Chaque mois"),
"rep_yearly": ("Каждый год", "Every year", "Jedes Jahr", "Chaque année"),
"rep_everyn": ("Через определённое количество дней", "Every N days", "Alle N Tage", "Tous les N jours"),
"rep_every_label": ("Каждые", "Every", "Alle", "Tous les"),
"rep_days_label": ("дн.", "days", "Tage", "jours"),
"rep_every_n": ("Каждые {0} дн.", "Every {0} days", "Alle {0} Tage", "Tous les {0} jours"),
# --- тихий режим ---
"quiet_title": ("Тихий режим", "Quiet mode", "Ruhemodus", "Mode silencieux"),
"quiet_hint": ("Звуковые сигналы будут отключены на выбранное время. Визуальные уведомления сохранятся.", "Sounds will be muted for the selected time. Visual notifications stay on.", "Signaltöne werden für den gewählten Zeitraum ausgeschaltet. Visuelle Benachrichtigungen bleiben aktiv.", "Les sons seront coupés pendant la durée choisie. Les notifications visuelles restent actives."),
"quiet_30": ("На 30 минут", "For 30 minutes", "30 Minuten", "Pendant 30 minutes"),
"quiet_60": ("На 1 час", "For 1 hour", "1 Stunde", "Pendant 1 heure"),
"quiet_120": ("На 2 часа", "For 2 hours", "2 Stunden", "Pendant 2 heures"),
"quiet_240": ("На 4 часа", "For 4 hours", "4 Stunden", "Pendant 4 heures"),
"quiet_morning": ("До 8:00 утра", "Until 8:00 AM", "Bis 8:00 Uhr morgens", "Jusqu’à 8 h demain"),
"quiet_off": ("Выключить тихий режим", "Turn quiet mode off", "Ruhemodus ausschalten", "Désactiver le mode silencieux"),
"quiet_on": ("тихий режим", "quiet mode", "Ruhemodus", "mode silencieux"),
"quiet_until": ("Тихий режим до {0}", "Quiet mode until {0}", "Ruhemodus bis {0}", "Mode silencieux jusqu’à {0}"),
"tray_open": ("Открыть планировщик", "Open planner", "Planer öffnen", "Ouvrir le planificateur"),
"tray_add": ("Добавить задачу", "Add task", "Aufgabe hinzufügen", "Ajouter une tâche"),
"tray_quiet": ("Тихий режим", "Quiet mode", "Ruhemodus", "Mode silencieux"),
"tray_exit": ("Выход", "Exit", "Beenden", "Quitter"),
# --- капелька и напоминания ---
"droplet_window": ("Скоро событие", "Upcoming event", "Bald ein Termin", "Événement proche"),
"due_window": ("Напоминание", "Reminder", "Erinnerung", "Rappel"),
"droplet_title": ("У ВАС ЗАПЛАНИРОВАНО СОБЫТИЕ!", "YOU HAVE AN EVENT PLANNED!", "SIE HABEN EINEN TERMIN GEPLANT!", "VOUS AVEZ UN ÉVÉNEMENT PRÉVU !"),
"droplet_body": ("Пожалуйста, не пропустите запланированное дело и подготовьтесь к его выполнению.", "Please don’t miss your planned task and get ready for it.", "Bitte verpassen Sie Ihren geplanten Termin nicht und bereiten Sie sich darauf vor.", "Merci de ne pas manquer votre tâche prévue et de vous y préparer."),
"droplet_time": ("Время события: {0}.", "Event time: {0}.", "Uhrzeit des Termins: {0}.", "Heure de l’événement : {0}."),
"droplet_name": ("Название: {0}", "Title: {0}", "Titel: {0}", "Titre : {0}"),
"due_title": ("ПОРА! НАСТУПИЛО ВРЕМЯ СОБЫТИЯ", "IT’S TIME! YOUR EVENT IS NOW", "ES IST SOWEIT! IHR TERMIN BEGINNT", "C’EST L’HEURE ! VOTRE ÉVÉNEMENT COMMENCE"),
"more_tasks": ("Другие задачи на этот день:", "Other tasks for this day:", "Weitere Aufgaben an diesem Tag:", "Autres tâches ce jour-là :"),
"toast_due_title": ("Напоминание", "Reminder", "Erinnerung", "Rappel"),
"toast_missed_title": ("Пропущенные события", "Missed events", "Verpasste Termine", "Événements manqués"),
"toast_missed": ("Пока программа не работала, наступило событий: {0}", "Events that came up while the app was not running: {0}", "Termine, die eintraten, während das Programm nicht lief: {0}", "Événements survenus pendant que l’application ne tournait pas : {0}"),
# --- история ---
"history_title": ("История", "History", "Verlauf", "Historique"),
"hist_done": ("Выполненные", "Completed", "Erledigt", "Terminées"),
"hist_overdue": ("Просроченные", "Overdue", "Überfällig", "En retard"),
"hist_deleted": ("Удалённые", "Deleted", "Gelöscht", "Supprimées"),
"hist_completed_at": ("Выполнено: {0}", "Completed: {0}", "Erledigt: {0}", "Terminée : {0}"),
"hist_due_at": ("Срок: {0}", "Due: {0}", "Fällig: {0}", "Échéance : {0}"),
"msg_confirm_delete_forever": ("Удалить задачу «{0}» навсегда?", "Delete the task “{0}” permanently?", "Aufgabe „{0}“ endgültig löschen?", "Supprimer définitivement la tâche « {0} » ?"),
"msg_confirm_clear": ("Удалить все задачи из этого списка навсегда?", "Permanently delete all tasks in this list?", "Alle Aufgaben in dieser Liste endgültig löschen?", "Supprimer définitivement toutes les tâches de cette liste ?"),
# --- голосовой ввод ---
"voice_title": ("Голосовое создание задачи", "Create a task by voice", "Aufgabe per Sprache erstellen", "Créer une tâche à la voix"),
"voice_recognized": ("Распознанный текст (можно исправить)", "Recognized text (you can edit it)", "Erkannter Text (kann korrigiert werden)", "Texte reconnu (modifiable)"),
"voice_examples": ("Например: «Завтра в три часа позвонить врачу» или «Каждый понедельник в девять утра проверить почту».", "For example: “Tomorrow at 3 pm call the doctor” or “Every Monday at 9 am check email”.", "Zum Beispiel: „Morgen um 15 Uhr Arzt anrufen“ oder „Jeden Montag um 9 Uhr E-Mails prüfen“.", "Par exemple : « Demain à 15 h appeler le médecin » ou « Chaque lundi à 9 h vérifier les e-mails »."),
"voice_result": ("Результат распознавания", "Recognition result", "Erkennungsergebnis", "Résultat de la reconnaissance"),
"voice_line_title": ("Задача: {0}", "Task: {0}", "Aufgabe: {0}", "Tâche : {0}"),
"voice_line_date": ("Дата: {0}", "Date: {0}", "Datum: {0}", "Date : {0}"),
"voice_line_time": ("Время: {0}", "Time: {0}", "Uhrzeit: {0}", "Heure : {0}"),
"voice_line_repeat": ("Повторение: {0}", "Repeat: {0}", "Wiederholung: {0}", "Répétition : {0}"),
"voice_no_time": ("не указано (будет 09:00)", "not specified (will be 09:00)", "nicht angegeben (wird 09:00)", "non précisée (09:00 par défaut)"),
"voice_ampm_question": ("Уточните время:", "Please confirm the time:", "Bitte Uhrzeit bestätigen:", "Veuillez confirmer l’heure :"),
"voice_confirm": ("Вы запланировали: {0}. Дата: {1}. Время: {2}. Сохранить задачу?", "You planned: {0}. Date: {1}. Time: {2}. Save the task?", "Sie haben geplant: {0}. Datum: {1}. Uhrzeit: {2}. Aufgabe speichern?", "Vous avez prévu : {0}. Date : {1}. Heure : {2}. Enregistrer la tâche ?"),
"rel_today": ("сегодня", "today", "heute", "aujourd’hui"),
"rel_tomorrow": ("завтра", "tomorrow", "morgen", "demain"),
"speech_local_ok": ("Распознавание речи: локальное, интернет не нужен.", "Speech recognition: local, no internet needed.", "Spracherkennung: lokal, kein Internet nötig.", "Reconnaissance vocale : locale, pas besoin d’Internet."),
"speech_online_needed": ("Для этого языка нет установленного в Windows распознавателя речи. Будет использовано онлайн-распознавание Windows — нужен интернет и включённое «Распознавание голоса» в параметрах конфиденциальности.", "No speech recognizer is installed in Windows for this language. Windows online speech recognition will be used — it needs internet and “Online speech recognition” enabled in Windows privacy settings.", "Für diese Sprache ist in Windows kein Spracherkenner installiert. Es wird die Online-Spracherkennung von Windows verwendet – sie benötigt Internet und die Option „Onlinespracherkennung“ in den Windows-Datenschutzeinstellungen.", "Aucun module de reconnaissance vocale n’est installé dans Windows pour cette langue. La reconnaissance vocale en ligne de Windows sera utilisée : elle nécessite Internet et l’option « Reconnaissance vocale en ligne » dans les paramètres de confidentialité."),
"speech_listening": ("Слушаю… говорите.", "Listening… speak now.", "Ich höre zu … bitte sprechen.", "J’écoute… parlez."),
"speech_done": ("Речь распознана (локально).", "Speech recognized (locally).", "Sprache erkannt (lokal).", "Parole reconnue (localement)."),
"speech_done_online": ("Речь распознана (онлайн).", "Speech recognized (online).", "Sprache erkannt (online).", "Parole reconnue (en ligne)."),
"speech_nothing": ("Ничего не удалось распознать. Попробуйте ещё раз или введите текст вручную.", "Nothing was recognized. Try again or type the text manually.", "Es wurde nichts erkannt. Versuchen Sie es erneut oder geben Sie den Text manuell ein.", "Rien n’a été reconnu. Réessayez ou saisissez le texte manuellement."),
"speech_no_mic": ("Микрофон не найден или к нему нет доступа. Проверьте «Параметры → Конфиденциальность → Микрофон».", "No microphone found or access is blocked. Check Settings → Privacy → Microphone.", "Kein Mikrofon gefunden oder Zugriff gesperrt. Prüfen Sie Einstellungen → Datenschutz → Mikrofon.", "Aucun micro trouvé ou accès bloqué. Vérifiez Paramètres → Confidentialité → Microphone."),
"speech_unsupported": ("Распознавание этого языка недоступно. Добавьте язык в «Параметры → Время и язык» (с пакетом распознавания речи) или введите текст вручную.", "Recognition for this language is unavailable. Add the language in Settings → Time & language (with the speech pack) or type the text manually.", "Die Erkennung dieser Sprache ist nicht verfügbar. Fügen Sie die Sprache unter Einstellungen → Zeit und Sprache (mit Sprachpaket) hinzu oder geben Sie den Text manuell ein.", "La reconnaissance de cette langue est indisponible. Ajoutez la langue dans Paramètres → Heure et langue (avec le pack vocal) ou saisissez le texte manuellement."),
"speech_network": ("Нет подключения к интернету, а для этого языка нужно онлайн-распознавание.", "No internet connection, but this language needs online recognition.", "Keine Internetverbindung, diese Sprache benötigt aber Online-Erkennung.", "Pas de connexion Internet, or cette langue nécessite la reconnaissance en ligne."),
"speech_policy": ("В Windows выключено «Распознавание голоса». Включите его: Параметры → Конфиденциальность → Речь.", "Online speech recognition is turned off in Windows. Turn it on: Settings → Privacy → Speech.", "Die Onlinespracherkennung ist in Windows ausgeschaltet. Aktivieren Sie sie: Einstellungen → Datenschutz → Sprache.", "La reconnaissance vocale en ligne est désactivée dans Windows. Activez-la : Paramètres → Confidentialité → Voix."),
"voice_auto": ("Автоматически", "Automatic", "Automatisch", "Automatique"),
"tts_test": ("Вы запланировали позвонить врачу завтра в пятнадцать часов. Сохранить задачу?", "You planned to call the doctor tomorrow at three in the afternoon. Save the task?", "Sie haben geplant, morgen um fünfzehn Uhr den Arzt anzurufen. Aufgabe speichern?", "Vous avez prévu d’appeler le médecin demain à quinze heures. Enregistrer la tâche ?"),
"tts_no_voice": ("В Windows нет голоса для этого языка. Добавьте его: Параметры → Время и язык → Речь.", "No Windows voice is installed for this language. Add one: Settings → Time & language → Speech.", "Für diese Sprache ist keine Windows-Stimme installiert. Hinzufügen: Einstellungen → Zeit und Sprache → Sprache.", "Aucune voix Windows n’est installée pour cette langue. Ajoutez-en une : Paramètres → Heure et langue → Voix."),
# --- настройки ---
"settings_title": ("Настройки", "Settings", "Einstellungen", "Paramètres"),
"set_appearance": ("Внешний вид", "Appearance", "Darstellung", "Apparence"),
"set_language": ("Язык интерфейса", "Interface language", "Oberflächensprache", "Langue de l’interface"),
"set_voice": ("Голосовой ввод", "Voice input", "Spracheingabe", "Saisie vocale"),
"set_sound": ("Звуковые сигналы", "Sound signals", "Signaltöne", "Signaux sonores"),
"set_reminders": ("Напоминания", "Reminders", "Erinnerungen", "Rappels"),
"set_droplet": ("Предупреждение-капелька", "Droplet warning", "Tropfen-Vorwarnung", "Alerte goutte"),
"set_weather": ("Погода", "Weather", "Wetter", "Météo"),
"set_quiet": ("Тихие часы", "Quiet hours", "Ruhezeiten", "Heures calmes"),
"set_autostart": ("Автозапуск", "Autostart", "Autostart", "Démarrage automatique"),
"set_backup": ("Резервное копирование", "Backup", "Datensicherung", "Sauvegarde"),
"set_general": ("Общие настройки", "General", "Allgemein", "Général"),
"lbl_theme": ("Тема оформления", "Theme", "Design", "Thème"),
"theme_system": ("Как в Windows", "Same as Windows", "Wie Windows", "Comme Windows"),
"theme_light": ("Светлая", "Light", "Hell", "Clair"),
"theme_dark": ("Тёмная", "Dark", "Dunkel", "Sombre"),
"lbl_ui_language": ("Язык программы", "Program language", "Programmsprache", "Langue du programme"),
"note_language": ("Язык меняется сразу, без перезапуска. Ваши задачи не переводятся — переводится только интерфейс. Чтобы добавить язык, положите файл перевода (например, es-ES.json) в папку Locales.", "The language changes immediately, no restart needed. Your tasks are not translated — only the interface is. To add a language, place a translation file (e.g. es-ES.json) in the Locales folder.", "Die Sprache wechselt sofort, ohne Neustart. Ihre Aufgaben werden nicht übersetzt – nur die Oberfläche. Um eine Sprache hinzuzufügen, legen Sie eine Übersetzungsdatei (z. B. es-ES.json) in den Ordner Locales.", "La langue change immédiatement, sans redémarrage. Vos tâches ne sont pas traduites : seule l’interface l’est. Pour ajouter une langue, placez un fichier de traduction (par ex. es-ES.json) dans le dossier Locales."),
"lbl_speech_language": ("Язык голосового ввода", "Dictation language", "Sprache der Diktierfunktion", "Langue de dictée"),
"note_speech_install": ("Язык диктовки выбирается отдельно от языка интерфейса. Для локального распознавания языковой пакет с распознаванием речи должен быть установлен в Windows: Параметры → Время и язык → Язык и регион.", "The dictation language is chosen separately from the interface language. For local recognition the language pack with speech recognition must be installed in Windows: Settings → Time & language → Language & region.", "Die Diktiersprache wird unabhängig von der Oberflächensprache gewählt. Für lokale Erkennung muss das Sprachpaket mit Spracherkennung in Windows installiert sein: Einstellungen → Zeit und Sprache → Sprache und Region.", "La langue de dictée se choisit séparément de celle de l’interface. Pour la reconnaissance locale, le pack de langue avec reconnaissance vocale doit être installé dans Windows : Paramètres → Heure et langue → Langue et région."),
"lbl_tts_enabled": ("Озвучивать распознанную задачу для подтверждения", "Read the recognized task aloud for confirmation", "Erkannte Aufgabe zur Bestätigung vorlesen", "Lire la tâche reconnue à voix haute pour confirmation"),
"lbl_tts_voice": ("Голос", "Voice", "Stimme", "Voix"),
"lbl_tts_rate": ("Скорость речи", "Speech rate", "Sprechgeschwindigkeit", "Vitesse de la voix"),
"lbl_tts_volume": ("Громкость речи", "Speech volume", "Lautstärke der Sprache", "Volume de la voix"),
"lbl_sound_enabled": ("Включить звуковые сигналы", "Enable sound signals", "Signaltöne aktivieren", "Activer les signaux sonores"),
"lbl_sound_choice": ("Сигнал", "Signal", "Signal", "Signal"),
"snd_soft": ("Мягкий (по умолчанию)", "Soft (default)", "Sanft (Standard)", "Doux (par défaut)"),
"snd_bell": ("Колокольчик", "Bell", "Glöckchen", "Clochette"),
"snd_marimba": ("Маримба", "Marimba", "Marimba", "Marimba"),
"lbl_volume": ("Громкость сигнала", "Signal volume", "Signallautstärke", "Volume du signal"),
"lbl_repeat_interval": ("Повторять сигнал каждые (минут), пока задача не выполнена", "Repeat the signal every (minutes) until the task is done", "Signal alle (Minuten) wiederholen, bis die Aufgabe erledigt ist", "Répéter le signal toutes les (minutes) jusqu’à ce que la tâche soit terminée"),
"err_interval_minutes": ("Укажите интервал повтора от 1 до 720 минут.", "Enter a repeat interval between 1 and 720 minutes.", "Geben Sie ein Wiederholungsintervall von 1 bis 720 Minuten ein.", "Saisissez un intervalle de répétition entre 1 et 720 minutes."),
"rem_how": ("Перед событием появляется капелька на календаре (по умолчанию за 2 часа). В назначенное время звучит мягкий сигнал и показывается уведомление. Если задача не выполнена, сигнал повторяется (по умолчанию каждые 10 минут) — до выполнения, отключения напоминания или переноса задачи.", "Before an event a droplet appears on the calendar (2 hours ahead by default). At the scheduled time a soft signal plays and a notification appears. If the task is not done, the signal repeats (every 10 minutes by default) until you complete it, turn its reminder off or reschedule it.", "Vor einem Termin erscheint ein Tropfen im Kalender (standardmäßig 2 Stunden vorher). Zur festgelegten Zeit ertönt ein sanftes Signal und eine Benachrichtigung erscheint. Ist die Aufgabe nicht erledigt, wiederholt sich das Signal (standardmäßig alle 10 Minuten), bis Sie sie erledigen, die Erinnerung ausschalten oder verschieben.", "Avant un événement, une goutte apparaît dans le calendrier (2 heures avant par défaut). À l’heure prévue, un signal doux retentit et une notification s’affiche. Si la tâche n’est pas terminée, le signal se répète (toutes les 10 minutes par défaut) jusqu’à ce que vous la terminiez, désactiviez son rappel ou la reportiez."),
"rem_missed": ("События, пропущенные из-за выключенного компьютера или сна, показываются одним общим уведомлением — без серии сигналов подряд.", "Events missed while the computer was off or asleep are shown in one combined notification — without a burst of signals.", "Termine, die bei ausgeschaltetem oder schlafendem Computer verpasst wurden, erscheinen in einer gemeinsamen Benachrichtigung – ohne Signalserie.", "Les événements manqués pendant l’arrêt ou la veille de l’ordinateur s’affichent dans une seule notification, sans rafale de signaux."),
"lbl_droplet_enabled": ("Показывать капельку на календаре", "Show the droplet on the calendar", "Tropfen im Kalender anzeigen", "Afficher la goutte dans le calendrier"),
"lbl_droplet_before": ("Предупреждать за", "Warn", "Vorwarnen", "Prévenir"),
"lbl_droplet_text": ("Текст предупреждения (можно изменить)", "Warning text (editable)", "Warntext (änderbar)", "Texte de l’alerte (modifiable)"),
"note_droplet_text": ("Заголовок, время и название задачи добавляются автоматически.", "The heading, time and task title are added automatically.", "Überschrift, Uhrzeit und Aufgabentitel werden automatisch ergänzt.", "Le titre, l’heure et le nom de la tâche sont ajoutés automatiquement."),
"err_warn_minutes": ("Укажите интервал от 5 до 2880 минут.", "Enter an interval between 5 and 2880 minutes.", "Geben Sie ein Intervall von 5 bis 2880 Minuten ein.", "Saisissez un intervalle entre 5 et 2880 minutes."),
"lbl_city": ("Город", "City", "Stadt", "Ville"),
"note_weather": ("Город вводится вручную, например «Владивосток» или «Berlin». Для погоды нужен интернет (сервис Open-Meteo); без интернета показываются последние полученные данные.", "Enter the city manually, e.g. “Berlin” or “Paris”. Weather needs internet (Open-Meteo service); offline, the last received data is shown.", "Geben Sie die Stadt manuell ein, z. B. „Berlin“ oder „Paris“. Das Wetter benötigt Internet (Dienst Open-Meteo); offline werden die zuletzt empfangenen Daten angezeigt.", "Saisissez la ville manuellement, par ex. « Paris » ou « Berlin ». La météo nécessite Internet (service Open-Meteo) ; hors ligne, les dernières données reçues sont affichées."),
"weather_loading": ("Загрузка…", "Loading…", "Wird geladen …", "Chargement…"),
"weather_set_city": ("Укажите город в настройках", "Set a city in settings", "Stadt in den Einstellungen festlegen", "Indiquez une ville dans les paramètres"),
"weather_no_data": ("Нет данных о погоде", "No weather data", "Keine Wetterdaten", "Pas de données météo"),
"weather_not_found": ("Город не найден", "City not found", "Stadt nicht gefunden", "Ville introuvable"),
"weather_offline": ("{0} (нет сети, данные от {1})", "{0} (offline, data from {1})", "{0} (offline, Stand {1})", "{0} (hors ligne, données du {1})"),
"weather_updated": ("Обновлено: {0}", "Updated: {0}", "Aktualisiert: {0}", "Mis à jour : {0}"),
"weather_offline_note": ("Нет подключения — показаны последние сохранённые данные.", "No connection — showing the last saved data.", "Keine Verbindung – es werden die zuletzt gespeicherten Daten angezeigt.", "Pas de connexion : affichage des dernières données enregistrées."),
"wx_clear": ("Ясно", "Clear", "Klar", "Dégagé"),
"wx_partly": ("Переменная облачность", "Partly cloudy", "Teils bewölkt", "Partiellement nuageux"),
"wx_cloudy": ("Облачно", "Cloudy", "Bewölkt", "Nuageux"),
"wx_fog": ("Туман", "Fog", "Nebel", "Brouillard"),
"wx_drizzle": ("Морось", "Drizzle", "Nieselregen", "Bruine"),
"wx_rain": ("Дождь", "Rain", "Regen", "Pluie"),
"wx_snow": ("Снег", "Snow", "Schnee", "Neige"),
"wx_showers": ("Ливень", "Showers", "Schauer", "Averses"),
"wx_thunder": ("Гроза", "Thunderstorm", "Gewitter", "Orage"),
"wx_unknown": ("—", "—", "—", "—"),
"lbl_quiet_enabled": ("Включить тихие часы (звук отключён, визуальные уведомления остаются)", "Enable quiet hours (sound off, visual notifications stay)", "Ruhezeiten aktivieren (kein Ton, visuelle Benachrichtigungen bleiben)", "Activer les heures calmes (son coupé, notifications visuelles conservées)"),
"lbl_from": ("с", "from", "von", "de"),
"lbl_to": ("до", "to", "bis", "à"),
"note_quiet": ("Время в формате ЧЧ:ММ. Период может переходить через полночь, например с 22:00 до 08:00.", "Time as HH:mm. The period may cross midnight, e.g. from 22:00 to 08:00.", "Uhrzeit als HH:mm. Der Zeitraum darf über Mitternacht gehen, z. B. von 22:00 bis 08:00.", "Heure au format HH:mm. La période peut passer minuit, par ex. de 22:00 à 08:00."),
"lbl_autostart": ("Запускать вместе с Windows", "Start with Windows", "Mit Windows starten", "Démarrer avec Windows"),
"lbl_start_minimized": ("При автозапуске сворачивать в системный трей", "When autostarted, minimize to the system tray", "Beim Autostart in den Infobereich minimieren", "Au démarrage automatique, réduire dans la zone de notification"),
"lbl_backup_enabled": ("Автоматически делать резервную копию раз в сутки", "Automatically back up once a day", "Täglich automatisch sichern", "Sauvegarder automatiquement une fois par jour"),
"lbl_backup_keep": ("Сколько копий хранить", "Number of backups to keep", "Anzahl der aufbewahrten Sicherungen", "Nombre de sauvegardes à conserver"),
"err_keep": ("Укажите число копий от 1 до 100.", "Enter a number of backups between 1 and 100.", "Geben Sie eine Anzahl von 1 bis 100 ein.", "Saisissez un nombre de sauvegardes entre 1 et 100."),
"note_backup": ("Все изменения сохраняются сразу. Копии лежат в папке данных программы.", "All changes are saved immediately. Backups are stored in the program’s data folder.", "Alle Änderungen werden sofort gespeichert. Sicherungen liegen im Datenordner des Programms.", "Toutes les modifications sont enregistrées immédiatement. Les sauvegardes sont dans le dossier de données du programme."),
"msg_backup_done": ("Резервная копия создана:\n{0}", "Backup created:\n{0}", "Sicherung erstellt:\n{0}", "Sauvegarde créée :\n{0}"),
"msg_backup_failed": ("Не удалось создать резервную копию.", "Could not create the backup.", "Die Sicherung konnte nicht erstellt werden.", "Impossible de créer la sauvegarde."),
"msg_confirm_restore": ("Заменить текущие данные данными из копии? Перед этим будет создана копия текущего состояния.", "Replace the current data with the backup? A backup of the current state will be made first.", "Aktuelle Daten durch die Sicherung ersetzen? Zuvor wird der aktuelle Stand gesichert.", "Remplacer les données actuelles par la sauvegarde ? Une sauvegarde de l’état actuel sera créée avant."),
"msg_restore_done": ("Данные восстановлены.", "Data restored.", "Daten wiederhergestellt.", "Données restaurées."),
"msg_restore_failed": ("Не удалось восстановить данные из копии.", "Could not restore the backup.", "Die Sicherung konnte nicht wiederhergestellt werden.", "Impossible de restaurer la sauvegarde."),
"lbl_close_to_tray": ("Кнопка «Закрыть» убирает окно в системный трей (а не завершает программу)", "The “Close” button hides the window to the system tray (instead of quitting)", "„Schließen“ blendet das Fenster in den Infobereich aus (statt zu beenden)", "Le bouton « Fermer » masque la fenêtre dans la zone de notification (au lieu de quitter)"),
"lbl_min_to_tray": ("Сворачивать в трей вместо панели задач", "Minimize to the tray instead of the taskbar", "In den Infobereich statt in die Taskleiste minimieren", "Réduire dans la zone de notification plutôt que dans la barre des tâches"),
"about_version": ("Мой планировщик, версия {0}", "My Planner, version {0}", "Mein Planer, Version {0}", "Mon planificateur, version {0}"),
}

def build():
    os.makedirs(OUT, exist_ok=True)
    codes = list(META.keys())
    for idx, code in enumerate(codes):
        d = {"_native": META[code][0], "_badge": META[code][1]}
        for k, v in T.items():
            d[k] = v[idx]
        with open(os.path.join(OUT, code + ".json"), "w", encoding="utf-8") as f:
            json.dump(d, f, ensure_ascii=False, indent=2)
            f.write("\n")

def used_keys():
    keys = set()
    pat_xaml = re.compile(r"svc:T ([A-Za-z0-9_]+)")
    pat_lit = re.compile(r'"((?:btn|lbl|tip|note|set|msg|err|hist|wx|snd|cat|theme|warn|prio|rep|speech|voice|quiet|droplet|due|toast|tray|weather|dlg|rem|move|status|rel|tts|about|plans|no|app|more|settings|history)_[A-Za-z0-9_]+)"')
    for f in glob.glob(SRC + "/**/*.cs", recursive=True) + glob.glob(SRC + "/**/*.xaml", recursive=True):
        s = open(f, encoding="utf-8").read()
        keys |= set(pat_xaml.findall(s))
        keys |= set(pat_lit.findall(s))
    return keys

def check():
    ok = True
    tables = {}
    for code in META:
        tables[code] = json.load(open(os.path.join(OUT, code + ".json"), encoding="utf-8"))
    base = set(tables["ru-RU"])
    for code, t in tables.items():
        miss = base - set(t)
        extra = set(t) - base
        empty = [k for k, v in t.items() if not str(v).strip()]
        if miss or extra or empty:
            ok = False
            print(code, "missing:", miss, "extra:", extra, "empty:", empty)
        # одинаковые плейсхолдеры {0},{1}
        for k in base:
            a = sorted(re.findall(r"\{\d\}", tables["ru-RU"][k]))
            b = sorted(re.findall(r"\{\d\}", t.get(k, "")))
            if a != b:
                ok = False
                print(code, "placeholder mismatch in", k, a, b)
    used = used_keys()
    IGNORE = {"weather_code"}  # параметр URL Open-Meteo, не ключ перевода
    undefined = sorted(k for k in used if k not in base and k not in IGNORE)
    if undefined:
        ok = False
        print("Keys used in code but not defined:", undefined)
    unused = sorted(k for k in base if k not in used and not k.startswith("_"))
    if unused:
        print("(info) defined but not referenced literally:", unused)
    print("keys:", len(base), "used literal:", len(used), "OK" if ok else "PROBLEMS")
    return ok

if __name__ == "__main__":
    build()
    sys.exit(0 if check() else 1)
