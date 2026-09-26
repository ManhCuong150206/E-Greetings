document.addEventListener("DOMContentLoaded", function () {
    const cardArea = document.getElementById("card-preview-area");
    const inputMessage = document.getElementById("input-message");
    const selectFont = document.getElementById("select-font");
    const inputColor = document.getElementById("input-color");
    const selectAudio = document.getElementById("select-audio");
    const audioPlayer = document.getElementById("audio-player");
    const btnSaveCard = document.getElementById("btn-save-card");
    const btnAddText = document.getElementById("btn-add-text");
    const btnClearCanvas = document.getElementById("btn-clear-canvas");

    let activeElement = document.querySelector(".draggable-element");

    // 1. KHỞI TẠO TÍNH NĂNG KÉO THẢ (DRAG & DROP)
    function makeDraggable(element) {
        let isDragging = false;
        let offsetX = 0, offsetY = 0;

        element.addEventListener("mousedown", function (e) {
            isDragging = true;
            activeElement = element;

            // Đổi viền để nhận biết vật thể đang chọn
            document.querySelectorAll(".draggable-element").forEach(el => el.style.outline = "none");
            element.style.outline = "2px dashed #0d6efd";

            // Nếu là khung chữ thì đồng bộ nội dung ra ô gõ text
            const textSpan = element.querySelector(".text-content");
            if (textSpan) {
                inputMessage.value = textSpan.textContent;
            }

            const rect = element.getBoundingClientRect();
            offsetX = e.clientX - rect.left;
            offsetY = e.clientY - rect.top;
        });

        document.addEventListener("mousemove", function (e) {
            if (!isDragging || activeElement !== element) return;

            const containerRect = cardArea.getBoundingClientRect();
            let x = e.clientX - containerRect.left - offsetX;
            let y = e.clientY - containerRect.top - offsetY;

            // Giới hạn không cho kéo văng ra ngoài khung thiệp
            x = Math.max(0, Math.min(x, containerRect.width - element.offsetWidth));
            y = Math.max(0, Math.min(y, containerRect.height - element.offsetHeight));

            element.style.left = x + "px";
            element.style.top = y + "px";
        });

        document.addEventListener("mouseup", function () {
            isDragging = false;
        });
    }

    // Áp dụng kéo thả cho phần tử mặc định ban đầu
    if (activeElement) makeDraggable(activeElement);

    // 2. TỰ ĐỘNG CẬP NHẬT CHỮ / FONT / MÀU SẮC CHO PHẦN TỬ ĐANG CHỌN
    inputMessage.addEventListener("input", function () {
        if (activeElement) {
            const textSpan = activeElement.querySelector(".text-content");
            if (textSpan) textSpan.textContent = this.value || "Lời chúc...";
        }
    });

    selectFont.addEventListener("change", function () {
        if (activeElement) activeElement.style.fontFamily = this.value;
    });

    inputColor.addEventListener("input", function () {
        if (activeElement) activeElement.style.color = this.value;
    });

    // 3. THÊM KHUNG CHỮ MỚI
    btnAddText.addEventListener("click", function () {
        const newDiv = document.createElement("div");
        newDiv.className = "draggable-element position-absolute p-1 text-center";
        newDiv.style.cssText = "top: 20px; left: 20px; color: #000; font-family: Arial; font-size: 18px; cursor: move; user-select: none; background: rgba(255,255,255,0.5);";
        newDiv.innerHTML = `<span class="text-content">Lời chúc mới</span>`;

        cardArea.appendChild(newDiv);
        makeDraggable(newDiv);
        newDiv.dispatchEvent(new Event("mousedown"));
    });

    // 4. CHỌN VÀ THÊM STICKER
    document.querySelectorAll(".sticker-opt").forEach(sticker => {
        sticker.addEventListener("click", function () {
            const emoji = this.textContent;
            const stickerDiv = document.createElement("div");
            stickerDiv.className = "draggable-element position-absolute";
            stickerDiv.style.cssText = "top: 50px; left: 50px; font-size: 35px; cursor: move; user-select: none;";
            stickerDiv.innerHTML = `<span>${emoji}</span>`;

            cardArea.appendChild(stickerDiv);
            makeDraggable(stickerDiv);
        });
    });

    // 5. XÓA VẬT THỂ ĐANG CHỌN
    btnClearCanvas.addEventListener("click", function () {
        if (activeElement && cardArea.children.length > 1) {
            activeElement.remove();
            activeElement = null;
        }
    });

    // 6. PHÁT NHẠC
    selectAudio.addEventListener("change", function () {
        if (this.value) {
            audioPlayer.src = this.value;
            audioPlayer.classList.remove("d-none");
            audioPlayer.play();
        } else {
            audioPlayer.pause();
            audioPlayer.classList.add("d-none");
        }
    });

    // 7. GỬI DỮ LIỆU VỀ BE (SaveCardConfig)
    btnSaveCard.addEventListener("click", async function () {
        const recipientEmail = document.getElementById("input-recipient").value.trim();
        const subject = document.getElementById("input-subject").value.trim();

        if (!recipientEmail) {
            alert("Vui lòng nhập Email người nhận!");
            return;
        }

        // Bỏ viền nét đứt đang chọn trên vật thể trước khi lưu
        document.querySelectorAll(".draggable-element").forEach(el => el.style.outline = "none");

        // Lấy toàn bộ HTML canvas
        const cardHtml = cardArea.innerHTML;

        const payload = {
            templateId: 1,
            recipientEmail: recipientEmail,
            subject: subject,
            audioUrl: selectAudio.value,
            cardHtmlContent: cardHtml
        };

        try {
            btnSaveCard.disabled = true;
            btnSaveCard.innerText = "Đang lưu...";

            const response = await fetch("/Card/SaveCardConfig", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            });

            const result = await response.json();
            if (result.success) {
                const fullCardLink = window.location.origin + result.cardUrl;

                // Tự động copy link thiệp vào clipboard
                try {
                    await navigator.clipboard.writeText(fullCardLink);
                } catch (e) {
                    console.log("Chưa cấp quyền clipboard:", e);
                }

                // Thông báo thành công
                alert("🎉 " + result.message + "\n\nLink thiệp đã được copy. Bấm OK để tới trang xem thiệp!");

                // 🚀 CHUYỂN TRANG NGAY TRÊN TAB HIỆN TẠI (KHÔNG MỞ TAB MỚI)
                window.location.href = result.cardUrl;
            } else {
                alert("Lỗi: " + result.message);
            }
        } catch (err) {
            alert("Lỗi kết nối Server!");
        } finally {
            btnSaveCard.disabled = false;
            btnSaveCard.innerText = "Lưu & Gửi Thiệp";
        }
    });
});