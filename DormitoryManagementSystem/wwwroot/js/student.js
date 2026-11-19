(() => {
    "use strict";

    const sidebar = document.querySelector("[data-student-sidebar]");
    const scrim = document.querySelector("[data-student-scrim]");

    const closeMenu = () => {
        sidebar?.classList.remove("open");
        scrim?.classList.remove("open");
    };

    document.querySelector("[data-student-menu]")?.addEventListener("click", () => {
        sidebar?.classList.toggle("open");
        scrim?.classList.toggle("open");
    });
    scrim?.addEventListener("click", closeMenu);
    sidebar?.querySelectorAll("a").forEach(link => link.addEventListener("click", closeMenu));

    document.querySelectorAll("[data-flash-close]").forEach(button => {
        button.addEventListener("click", () => button.closest(".student-flash")?.remove());
    });

    let toastTimer;
    const showToast = (message) => {
        let toast = document.querySelector("[data-student-toast]");
        if (!toast) {
            toast = document.createElement("div");
            toast.className = "student-toast";
            toast.dataset.studentToast = "";
            toast.setAttribute("role", "status");
            document.body.append(toast);
        }

        toast.textContent = message;
        toast.classList.add("show");
        window.clearTimeout(toastTimer);
        toastTimer = window.setTimeout(() => toast.classList.remove("show"), 4200);
    };

    document.querySelectorAll("form[data-confirm]").forEach(form => {
        form.addEventListener("submit", event => {
            if (!window.confirm(form.dataset.confirm)) {
                event.preventDefault();
            }
        });
    });

    const bindDialog = (dialog, openers) => {
        if (!dialog) return;

        openers.forEach(opener => opener.addEventListener("click", () => {
            if (!dialog.open) dialog.showModal();
            dialog.querySelector('input:not([type="hidden"]), select, textarea')?.focus();
        }));
        dialog.querySelectorAll("[data-dialog-close]").forEach(button => {
            button.addEventListener("click", () => dialog.close());
        });
        dialog.addEventListener("click", event => {
            if (event.target === dialog) dialog.close();
        });
    };

    const repairDialog = document.querySelector("[data-repair-dialog]");
    bindDialog(repairDialog, Array.from(document.querySelectorAll("[data-repair-open]")));
    if (repairDialog && (window.location.hash === "#new-request" || repairDialog.dataset.reopen === "true")) {
        repairDialog.showModal();
        const errors = repairDialog.querySelector(".validation-summary-errors");
        (errors ?? repairDialog.querySelector("select"))?.focus();
    }

    const attachmentInput = repairDialog?.querySelector("[data-attachment-input]");
    if (attachmentInput) {
        const error = repairDialog.querySelector("[data-attachment-error]");
        const count = repairDialog.querySelector("[data-attachment-count]");
        const previews = repairDialog.querySelector("[data-attachment-previews]");
        const clear = repairDialog.querySelector("[data-attachment-clear]");
        const supportedTypes = new Set(attachmentInput.accept.split(","));
        const maximumFiles = Number(attachmentInput.dataset.maximumFiles);
        const maximumSize = Number(attachmentInput.dataset.maximumSize);
        let previewUrls = [];

        const isSupportedImage = (file) => supportedTypes.has(file.type)
            || ((!file.type || file.type === "application/octet-stream") && /\.(jpe?g|png|webp)$/i.test(file.name));

        const selectionError = (files) => {
            if (files.length > maximumFiles) return `Chỉ chọn tối đa ${maximumFiles} ảnh mỗi lần gửi.`;
            const emptyFile = files.find(file => file.size === 0);
            if (emptyFile) return `Ảnh “${emptyFile.name}” không có dữ liệu. Hãy chọn lại.`;
            const largeFile = files.find(file => file.size > maximumSize);
            if (largeFile) return `Ảnh “${largeFile.name}” vượt quá 5 MB. Hãy chọn lại.`;
            const unsupportedFile = files.find(file => !isSupportedImage(file));
            return unsupportedFile ? `Tệp “${unsupportedFile.name}” cần ở định dạng JPG, PNG hoặc WebP.` : "";
        };

        const renderSelection = () => {
            previewUrls.forEach(url => URL.revokeObjectURL(url));
            previewUrls = [];
            previews.replaceChildren();
            previews.hidden = true;
            const files = Array.from(attachmentInput.files);
            const message = selectionError(files);
            attachmentInput.setCustomValidity(message);
            if (message) attachmentInput.setAttribute("aria-invalid", "true");
            else attachmentInput.removeAttribute("aria-invalid");
            error.textContent = message;
            error.hidden = !message;
            count.textContent = "";
            clear.hidden = !message && (!files.length || typeof DataTransfer === "function");
            if (message || !files.length) return;

            count.textContent = `Đã chọn ${files.length}/${maximumFiles} ảnh`;
            previews.hidden = false;
            files.forEach((file, index) => {
                const item = document.createElement("li");
                const thumbnail = document.createElement("img");
                const name = document.createElement("span");
                const url = URL.createObjectURL(file);
                previewUrls.push(url);
                thumbnail.src = url;
                thumbnail.alt = "";
                thumbnail.addEventListener("error", () => { thumbnail.hidden = true; }, { once: true });
                name.textContent = file.name;
                name.title = file.name;
                item.append(thumbnail, name);

                if (typeof DataTransfer === "function") {
                    const remove = document.createElement("button");
                    remove.type = "button";
                    remove.textContent = "Bỏ";
                    remove.setAttribute("aria-label", `Bỏ ảnh ${file.name}`);
                    remove.addEventListener("click", () => {
                        const remaining = new DataTransfer();
                        files.filter((_, fileIndex) => fileIndex !== index).forEach(kept => remaining.items.add(kept));
                        attachmentInput.files = remaining.files;
                        renderSelection();
                        const buttons = previews.querySelectorAll("button");
                        (buttons[Math.min(index, buttons.length - 1)] ?? attachmentInput).focus();
                    });
                    item.append(remove);
                }
                previews.append(item);
            });
        };

        attachmentInput.addEventListener("change", renderSelection);
        clear.addEventListener("click", () => {
            attachmentInput.value = "";
            renderSelection();
            attachmentInput.focus();
        });
        renderSelection();
    }

    document.querySelectorAll("[data-open-dialog]").forEach(opener => {
        const dialog = document.getElementById(opener.dataset.openDialog);
        if (dialog) bindDialog(dialog, [opener]);
    });

    const chatDialog = document.querySelector("[data-ai-assistant]");
    const chatMessages = document.querySelector("[data-chat-messages]");
    const chatWelcome = document.querySelector("[data-chat-welcome]");
    const chatInput = document.querySelector("[data-chat-input]");
    const chatForm = document.querySelector("[data-chat-form]");
    const chatActions = {
        rooms: "Mở Tìm phòng", applications: "Mở Đăng ký của tôi", invoices: "Xem hóa đơn",
        repairs: "Mở Yêu cầu hỗ trợ", notifications: "Xem thông báo", password: "Đổi mật khẩu",
        contact: "Xem thông tin liên hệ"
    };
    let chatBusy = false;
    let previousChatAction = null;

    const fitChatInput = () => {
        if (!(chatInput instanceof HTMLTextAreaElement)) return;
        chatInput.style.height = "auto";
        chatInput.style.height = `${Math.min(chatInput.scrollHeight, 104)}px`;
    };

    const openChat = () => {
        if (!chatDialog || chatDialog.open) return;
        chatDialog.showModal();
        fitChatInput();
        if (!matchMedia("(pointer: coarse)").matches) chatInput?.focus({ preventScroll: true });
    };

    document.querySelectorAll("[data-chat-open]").forEach(button => button.addEventListener("click", openChat));
    document.querySelector("[data-chat-close]")?.addEventListener("click", () => chatDialog?.close());

    const appendMessage = (kind, content, extraClass = "") => {
        const wrapper = document.createElement("div");
        wrapper.className = `ai-message ${kind} ${extraClass}`.trim();
        const paragraph = document.createElement("p");
        paragraph.textContent = content;
        wrapper.append(paragraph);
        chatMessages?.append(wrapper);
        if (chatMessages) chatMessages.scrollTop = chatMessages.scrollHeight;
        return wrapper;
    };

    const appendChatDetails = (message, answer) => {
        if (answer.source || answer.usedGemini) {
            const source = document.createElement("small");
            source.className = "ai-source";
            if (answer.source) {
                const label = answer.source.isDemo ? "Tài liệu minh họa" : "Nguồn";
                const reference = `${answer.source.document} · ${answer.source.section}`;
                source.textContent = `${label} · `;
                if (answer.source.url?.startsWith("https://")) {
                    const link = document.createElement("a");
                    link.href = answer.source.url;
                    link.target = "_blank";
                    link.rel = "noopener noreferrer";
                    link.textContent = reference;
                    source.append(link);
                } else {
                    source.append(reference);
                }
            } else {
                source.textContent = "Gemini · Hướng dẫn sử dụng · AI có thể nhầm";
            }
            message.append(source);
        }

        if (!Object.hasOwn(chatActions, answer.action)) return;
        const href = chatDialog?.dataset[answer.action];
        if (!href) return;
        const link = document.createElement("a");
        link.className = "ai-link";
        link.href = href;
        if (answer.action === "rooms" && answer.roomBuilding) {
            const roomUrl = new URL(href, window.location.href);
            roomUrl.searchParams.set("Building", answer.roomBuilding);
            link.href = roomUrl.pathname + roomUrl.search;
        }
        link.textContent = chatActions[answer.action];
        message.append(link);
    };

    const submitQuestion = async (question) => {
        const cleanQuestion = question.trim();
        if (!cleanQuestion || !chatMessages || !chatForm || chatBusy) return;
        if (cleanQuestion.length > 1000) {
            showToast("Câu hỏi không quá 1.000 ký tự.");
            return;
        }

        chatBusy = true;
        if (chatWelcome) chatWelcome.hidden = true;
        chatMessages.setAttribute("aria-busy", "true");
        appendMessage("user", cleanQuestion);
        const typing = appendMessage("assistant", "Đang trả lời…", "typing");
        const sendButton = chatForm.querySelector("button[type=submit]");
        if (sendButton) sendButton.disabled = true;
        const controller = new AbortController();
        const timeout = window.setTimeout(() => controller.abort(), 35000);

        try {
            const response = await fetch(chatForm.action, {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Content-Type": "application/json",
                    "RequestVerificationToken": chatForm.querySelector('input[name="__RequestVerificationToken"]')?.value ?? ""
                },
                body: JSON.stringify({ message: cleanQuestion, previousAction: previousChatAction }),
                signal: controller.signal
            });
            if (response.redirected || response.status === 401 || response.status === 403) {
                throw new Error("Phiên đăng nhập đã hết hạn. Bạn vui lòng đăng nhập lại.");
            }
            const answer = response.headers.get("content-type")?.includes("application/json")
                ? await response.json() : null;
            if (!response.ok || !answer?.success || typeof answer.text !== "string") {
                throw new Error(answer?.text || "Chưa thể nhận phản hồi. Bạn vui lòng tải lại trang rồi thử lại.");
            }
            typing.remove();
            const message = appendMessage("assistant", answer.text);
            appendChatDetails(message, answer);
            previousChatAction = Object.hasOwn(chatActions, answer.action) ? answer.action : null;
        } catch (error) {
            typing.remove();
            appendMessage("assistant", error.name === "AbortError"
                ? "Trợ lý phản hồi lâu hơn dự kiến. Bạn vui lòng thử lại."
                : error instanceof TypeError ? "Không thể kết nối. Bạn kiểm tra mạng rồi thử lại nhé." : error.message, "error");
            if (chatInput && !chatInput.value) {
                chatInput.value = cleanQuestion;
                if (chatDialog?.open) fitChatInput();
            }
        } finally {
            window.clearTimeout(timeout);
            chatBusy = false;
            chatMessages.setAttribute("aria-busy", "false");
            if (sendButton) sendButton.disabled = false;
            if (chatDialog?.open && !matchMedia("(pointer: coarse)").matches &&
                (document.activeElement === chatDialog || document.activeElement === document.body))
                chatInput?.focus({ preventScroll: true });
            if (chatMessages) chatMessages.scrollTop = chatMessages.scrollHeight;
        }
    };

    document.querySelectorAll("[data-chat-question]").forEach(button => {
        button.addEventListener("click", () => {
            openChat();
            submitQuestion(button.dataset.chatQuestion ?? "");
        });
    });

    document.querySelector("[data-chat-form]")?.addEventListener("submit", event => {
        event.preventDefault();
        if (chatBusy) return;
        const question = chatInput?.value ?? "";
        if (chatInput) {
            chatInput.value = "";
            fitChatInput();
        }
        submitQuestion(question);
    });

    chatInput?.addEventListener("input", fitChatInput);
    chatInput?.addEventListener("keydown", event => {
        if (event.key !== "Enter" || event.shiftKey || event.isComposing) return;
        event.preventDefault();
        if (!chatBusy && chatInput.value.trim()) chatForm?.requestSubmit();
    });
})();
