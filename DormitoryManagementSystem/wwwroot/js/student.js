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

    document.querySelectorAll("[data-demo-message]").forEach(element => {
        element.addEventListener("click", event => {
            event.preventDefault();
            showToast(element.dataset.demoMessage);
        });
    });

    document.querySelector("[data-student-notifications]")?.addEventListener("click", () => {
        showToast("2 thông báo mới: hóa đơn tháng 09 và yêu cầu sửa vòi nước đã được tiếp nhận.");
    });

    document.querySelectorAll("form[data-confirm]").forEach(form => {
        form.addEventListener("submit", event => {
            if (!window.confirm(form.dataset.confirm)) {
                event.preventDefault();
            }
        });
    });

    const bindDialog = (dialog, openers) => {
        if (!dialog) return;

        openers.forEach(opener => opener.addEventListener("click", () => dialog.showModal()));
        dialog.querySelectorAll("[data-dialog-close]").forEach(button => {
            button.addEventListener("click", () => dialog.close());
        });
        dialog.addEventListener("click", event => {
            if (event.target === dialog) dialog.close();
        });
    };

    const applicationDialog = document.querySelector("[data-application-dialog]");
    document.querySelectorAll("[data-apply-room]").forEach(button => {
        button.addEventListener("click", () => {
            if (!applicationDialog) return;
            applicationDialog.querySelector("[data-selected-room]").textContent = button.dataset.roomCode;
            applicationDialog.querySelector("[data-selected-room-id]").value = button.dataset.roomId;
            applicationDialog.showModal();
            applicationDialog.querySelector("textarea")?.focus();
        });
    });
    bindDialog(applicationDialog, []);

    const repairDialog = document.querySelector("[data-repair-dialog]");
    bindDialog(repairDialog, Array.from(document.querySelectorAll("[data-repair-open]")));
    if (repairDialog && window.location.hash === "#new-request") {
        repairDialog.showModal();
    }

    document.querySelectorAll("[data-open-dialog]").forEach(opener => {
        const dialog = document.getElementById(opener.dataset.openDialog);
        if (dialog) bindDialog(dialog, [opener]);
    });

    const chatDialog = document.querySelector("[data-ai-assistant]");
    const chatMessages = document.querySelector("[data-chat-messages]");
    const chatInput = document.querySelector("[data-chat-input]");

    const openChat = () => {
        if (!chatDialog) return;
        if (!chatDialog.open) chatDialog.showModal();
        window.setTimeout(() => chatInput?.focus(), 80);
    };

    document.querySelectorAll("[data-chat-open]").forEach(button => button.addEventListener("click", openChat));
    document.querySelector("[data-chat-close]")?.addEventListener("click", () => chatDialog?.close());

    const chatAnswer = (question) => {
        const normalized = question.toLocaleLowerCase("vi-VN");
        if (normalized.includes("đăng ký") || normalized.includes("phòng thế nào")) {
            return {
                text: "Vào Tìm phòng, lọc phòng phù hợp rồi chọn “Chọn phòng”. Bạn cần nêu lý do; đơn sẽ được ban quản lý kiểm tra trước khi duyệt.",
                source: "Quy trình đăng ký lưu trú 2026 · Mục 2 (dữ liệu demo)",
                href: "/Student/Rooms",
                label: "Mở trang Tìm phòng"
            };
        }
        if (normalized.includes("chuyển")) {
            return {
                text: "Chọn phòng mới còn chỗ và gửi đơn chuyển phòng. Trong lúc đơn đang xử lý, chỗ chưa được giữ và bạn vẫn ở phòng hiện tại.",
                source: "Quy trình chuyển phòng 2026 · Mục 3 (dữ liệu demo)",
                href: "/Student/Rooms",
                label: "Xem phòng còn chỗ"
            };
        }
        if (normalized.includes("sự cố") || normalized.includes("sửa") || normalized.includes("hỏng")) {
            return {
                text: "Vào Yêu cầu hỗ trợ, chọn nhóm sự cố, mức độ ảnh hưởng và mô tả rõ vị trí. Nếu nguy hiểm tức thời, hãy gọi trực ban 028 3896 5501.",
                source: "Quy trình tiếp nhận sửa chữa · Mục 1 (dữ liệu demo)",
                href: "/Student/Repairs#new-request",
                label: "Gửi yêu cầu hỗ trợ"
            };
        }
        if (normalized.includes("hóa đơn") || normalized.includes("phí") || normalized.includes("thanh toán")) {
            return {
                text: "Bạn có thể xem từng khoản trong mục Hóa đơn. Chỉ thanh toán theo hướng dẫn chính thức của KTX; trợ lý không tự suy đoán mức phí hoặc hạn nộp.",
                source: "Quy định thu phí KTX 2026 · Mục 4 (dữ liệu demo)",
                href: "/Student/Invoices",
                label: "Xem hóa đơn"
            };
        }

        return {
            text: "Mình chưa tìm thấy thông tin đã được xác nhận cho câu hỏi này. Bạn vui lòng liên hệ ban quản lý tại văn phòng KTX hoặc số 028 3896 5501.",
            source: "Chưa có tài liệu phù hợp trong kho dữ liệu demo"
        };
    };

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

    const submitQuestion = (question) => {
        const cleanQuestion = question.trim();
        if (!cleanQuestion || !chatMessages) return;

        appendMessage("user", cleanQuestion);
        const typing = appendMessage("assistant", "Đang tìm trong tài liệu đã xác nhận…", "typing");
        chatInput?.setAttribute("disabled", "disabled");

        window.setTimeout(() => {
            const answer = chatAnswer(cleanQuestion);
            typing.remove();
            const message = appendMessage("assistant", answer.text);
            const source = document.createElement("small");
            source.className = "ai-source";
            source.textContent = `Nguồn: ${answer.source}`;
            message.append(source);

            if (answer.href) {
                const link = document.createElement("a");
                link.className = "ai-link";
                link.href = answer.href;
                link.textContent = answer.label;
                message.append(link);
            }

            chatInput?.removeAttribute("disabled");
            chatInput?.focus();
            if (chatMessages) chatMessages.scrollTop = chatMessages.scrollHeight;
        }, 650);
    };

    document.querySelectorAll("[data-chat-question]").forEach(button => {
        button.addEventListener("click", () => {
            openChat();
            submitQuestion(button.dataset.chatQuestion ?? "");
        });
    });

    document.querySelector("[data-chat-form]")?.addEventListener("submit", event => {
        event.preventDefault();
        const question = chatInput?.value ?? "";
        if (chatInput) chatInput.value = "";
        submitQuestion(question);
    });
})();
