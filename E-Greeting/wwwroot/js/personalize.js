document.addEventListener("DOMContentLoaded", function () {
    // ==========================================
    // 1. KHAI BÁO CÁC PHẦN TỬ DOM
    // ==========================================
    const cardArea = document.getElementById("card-preview-area");
    const selectFont = document.getElementById("select-font");
    const selectFontSize = document.getElementById("select-fontsize");
    const inputColor = document.getElementById("input-color");
    const btnBold = document.getElementById("btn-bold");
    const btnItalic = document.getElementById("btn-italic");
    const selectAudio = document.getElementById("select-audio");
    const audioPlayer = document.getElementById("audio-player");
    const btnSaveCard = document.getElementById("btn-save-card");
    const btnAddText = document.getElementById("btn-add-text");
    const btnClearCanvas = document.getElementById("btn-clear-canvas");

    // Thanh trượt điều chỉnh độ rộng khung chữ
    const sliderTextWidth = document.getElementById("slider-text-width");
    const valTextWidth = document.getElementById("val-text-width");

    // Ô nhập liệu nội dung theo dòng (Template Fields)
    const tplSalutation = document.getElementById("tpl-salutation");
    const tplRecipient = document.getElementById("tpl-recipient");
    const tplMessage = document.getElementById("tpl-message");
    const tplSender = document.getElementById("tpl-sender");

    // Khai báo công cụ quản lý Ảnh
    const inputUploadImage = document.getElementById("input-upload-image");
    const imageTools = document.getElementById("image-tools");
    const sliderImgWidth = document.getElementById("slider-img-width");
    const sliderImgRadius = document.getElementById("slider-img-radius");
    const valImgWidth = document.getElementById("val-img-width");
    const valImgRadius = document.getElementById("val-img-radius");
    const btnImgCircle = document.getElementById("btn-img-circle");
    const btnSendBack = document.getElementById("btn-send-back");
    const btnBringFront = document.getElementById("btn-bring-front");

    // Vật thể đang được chọn (Chữ / Sticker / Ảnh)
    let activeElement = document.querySelector(".draggable-text-box, .draggable-element");

    // Lưu trữ đường dẫn base64 ảnh upload gần nhất
    let uploadedImagePath = "";

    // Hàm hỗ trợ chuyển mã màu RGB sang HEX
    function rgbToHex(rgbStr) {
        if (!rgbStr || rgbStr.startsWith('#')) return rgbStr || "#000000";
        const matches = rgbStr.match(/\d+/g);
        if (!matches || matches.length < 3) return '#000000';
        const r = parseInt(matches[0]).toString(16).padStart(2, '0');
        const g = parseInt(matches[1]).toString(16).padStart(2, '0');
        const b = parseInt(matches[2]).toString(16).padStart(2, '0');
        return `#${r}${g}${b}`;
    }

    // ==========================================
    // 2. TỰ ĐỘNG GHÉP NỘI DUNG TỪ CÁC Ô INPUT
    // ==========================================
    function buildTemplateText() {
        const salutation = tplSalutation?.value.trim() || "";
        const recipient = tplRecipient?.value.trim() || "";
        const message = tplMessage?.value.trim() || "";
        const sender = tplSender?.value.trim() || "";

        let lines = [];
        const line1 = [salutation, recipient].filter(Boolean).join(" ");
        if (line1) lines.push(line1);

        if (message) {
            if (lines.length > 0) lines.push("");
            lines.push(message);
        }

        if (sender) {
            if (lines.length > 0) lines.push("");
            lines.push(sender);
        }

        return lines.join("\n");
    }

    // ==========================================
    // 3. ĐỒNG BỘ CÔNG CỤ THEO VẬT THỂ ĐANG CHỌN
    // ==========================================
    function syncControlsWithElement(element) {
        if (!element) return;

        const imgTag = element.querySelector("img");

        if (imgTag) {
            // NẾU LÀ ẢNH: Hiện bảng công cụ ảnh
            if (imageTools) imageTools.classList.remove("d-none");

            const currentWidth = parseInt(imgTag.style.width) || imgTag.offsetWidth || 120;
            const currentRadius = parseInt(imgTag.style.borderRadius) || 0;

            if (sliderImgWidth) sliderImgWidth.value = currentWidth;
            if (valImgWidth) valImgWidth.innerText = currentWidth + "px";

            if (sliderImgRadius) sliderImgRadius.value = currentRadius;
            if (valImgRadius) valImgRadius.innerText = currentRadius + "%";
        } else {
            // NẾU LÀ CHỮ / STICKER: Ẩn công cụ ảnh
            if (imageTools) imageTools.classList.add("d-none");

            // Đồng bộ độ rộng khung chữ với thanh slider
            const currentTextWidth = parseInt(element.style.width) || element.offsetWidth || 300;
            if (sliderTextWidth) sliderTextWidth.value = currentTextWidth;
            if (valTextWidth) valTextWidth.innerText = currentTextWidth + "px";

            if (selectFont && element.style.fontFamily) {
                selectFont.value = element.style.fontFamily.replace(/['"]/g, '');
            }

            if (selectFontSize && element.style.fontSize) {
                selectFontSize.value = element.style.fontSize;
            }

            if (inputColor && element.style.color) {
                inputColor.value = rgbToHex(element.style.color);
            }

            if (btnBold) {
                const isBold = element.style.fontWeight === "bold" || element.style.fontWeight === "700";
                btnBold.classList.toggle("btn-primary", isBold);
                btnBold.classList.toggle("btn-outline-secondary", !isBold);
            }

            if (btnItalic) {
                const isItalic = element.style.fontStyle === "italic";
                btnItalic.classList.toggle("btn-primary", isItalic);
                btnItalic.classList.toggle("btn-outline-secondary", !isItalic);
            }
        }
    }

    // ==========================================
    // 4. KHỞI TẠO TÍNH NĂNG KÉO THẢ (DRAG & DROP - DESKTOP & MOBILE)
    // ==========================================
    function makeDraggable(element) {
        let isDragging = false;
        let offsetX = 0, offsetY = 0;

        const textSpan = element.querySelector(".text-content");
        if (textSpan) {
            textSpan.style.whiteSpace = "pre-wrap";
        }

        function onStart(e) {
            isDragging = true;
            activeElement = element;

            // Bỏ hiệu ứng chọn của tất cả các phần tử khác
            document.querySelectorAll(".draggable-text-box, .draggable-element").forEach(el => {
                el.classList.remove("active");
                el.style.outline = "none";
            });

            // Kích hoạt phần tử hiện tại
            element.classList.add("active");
            syncControlsWithElement(element);

            const clientX = e.touches ? e.touches[0].clientX : e.clientX;
            const clientY = e.touches ? e.touches[0].clientY : e.clientY;

            const rect = element.getBoundingClientRect();
            offsetX = clientX - rect.left;
            offsetY = clientY - rect.top;
        }

        function onMove(e) {
            if (!isDragging || activeElement !== element) return;

            const clientX = e.touches ? e.touches[0].clientX : e.clientX;
            const clientY = e.touches ? e.touches[0].clientY : e.clientY;

            const containerRect = cardArea.getBoundingClientRect();
            let x = clientX - containerRect.left - offsetX;
            let y = clientY - containerRect.top - offsetY;

            x = Math.max(0, Math.min(x, containerRect.width - element.offsetWidth));
            y = Math.max(0, Math.min(y, containerRect.height - element.offsetHeight));

            element.style.left = x + "px";
            element.style.top = y + "px";
        }

        function onEnd() {
            isDragging = false;
        }

        // Chuột (Desktop)
        element.addEventListener("mousedown", onStart);
        document.addEventListener("mousemove", onMove);
        document.addEventListener("mouseup", onEnd);

        // Cảm ứng (Mobile / Tablet)
        element.addEventListener("touchstart", onStart, { passive: true });
        document.addEventListener("touchmove", onMove, { passive: true });
        document.addEventListener("touchend", onEnd);
    }

    // Khởi tạo kéo thả cho vật thể mặc định ban đầu
    document.querySelectorAll(".draggable-text-box, .draggable-element").forEach(el => {
        makeDraggable(el);
    });

    // ==========================================
    // 5. ĐỌC DỮ LIỆU CẤU HÌNH BAN ĐẦU (CONFIG DATA)
    // ==========================================
    function applyInitialConfig() {
        if (!window.templateConfigData) return;
        const cfg = window.templateConfigData;

        // Cập nhật Nhạc nền nếu có trong Config
        const defaultAudio = cfg.audioPath || cfg.audio || cfg.music;
        if (defaultAudio && selectAudio) {
            selectAudio.value = defaultAudio;
            if (audioPlayer) {
                audioPlayer.src = defaultAudio;
                audioPlayer.classList.remove("d-none");
            }
        }

        // Đọc cấu hình style chữ mặc định nếu có
        if (activeElement && !activeElement.querySelector("img")) {
            if (cfg.fontFamily) activeElement.style.fontFamily = cfg.fontFamily;
            if (cfg.fontSize) activeElement.style.fontSize = cfg.fontSize;
            if (cfg.color) activeElement.style.color = cfg.color;
            if (cfg.fontWeight) activeElement.style.fontWeight = cfg.fontWeight;
            if (cfg.fontStyle) activeElement.style.fontStyle = cfg.fontStyle;
        }

        if (activeElement) {
            syncControlsWithElement(activeElement);
        }
    }

    applyInitialConfig();

    // ==========================================
    // 6. CẬP NHẬT CHỮ TỪ Ô NHẬP LIỆU
    // ==========================================
    function updateCardTextFromInputs() {
        if (!activeElement || activeElement.querySelector("img")) {
            activeElement = document.querySelector(".draggable-text-box, .draggable-element:not(:has(img))");
        }

        if (activeElement) {
            const textSpan = activeElement.querySelector(".text-content");
            if (textSpan) {
                textSpan.innerText = buildTemplateText();
            }
        }
    }

    document.querySelectorAll(".tpl-field").forEach(input => {
        input.addEventListener("input", updateCardTextFromInputs);
    });

    // Thay đổi độ rộng khung chữ bằng Thanh trượt (Slider)
    if (sliderTextWidth) {
        sliderTextWidth.addEventListener("input", function () {
            const newWidth = this.value + "px";
            if (valTextWidth) valTextWidth.innerText = newWidth;

            if (activeElement && !activeElement.querySelector("img")) {
                activeElement.style.width = newWidth;
            }
        });
    }

    // ==========================================
    // 7. THÊM VÀ TÙY CHỈNH ẢNH CÁ NHÂN
    // ==========================================
    if (inputUploadImage) {
        inputUploadImage.addEventListener("change", function (e) {
            const file = e.target.files[0];
            if (!file) return;

            const reader = new FileReader();
            reader.onload = function (event) {
                uploadedImagePath = event.target.result;

                const imgContainer = document.createElement("div");
                imgContainer.className = "draggable-element position-absolute d-inline-block";
                imgContainer.style.cssText = "top: 40px; left: 40px; cursor: move; user-select: none; z-index: 1;";

                const img = document.createElement("img");
                img.src = uploadedImagePath;
                img.style.cssText = "width: 120px; height: 120px; object-fit: cover; border-radius: 0%; display: block; pointer-events: none;";

                imgContainer.appendChild(img);
                cardArea.prepend(imgContainer);

                makeDraggable(imgContainer);
                imgContainer.dispatchEvent(new Event("mousedown"));
                inputUploadImage.value = "";
            };
            reader.readAsDataURL(file);
        });
    }

    // Biến ảnh thành Hình Tròn (50%)
    if (btnImgCircle) {
        btnImgCircle.addEventListener("click", function () {
            if (activeElement) {
                const img = activeElement.querySelector("img");
                if (img) {
                    const isCircle = img.style.borderRadius === "50%";
                    if (isCircle) {
                        img.style.borderRadius = "0%";
                        if (sliderImgRadius) sliderImgRadius.value = 0;
                        if (valImgRadius) valImgRadius.innerText = "0%";
                    } else {
                        img.style.borderRadius = "50%";
                        img.style.height = img.style.width;
                        if (sliderImgRadius) sliderImgRadius.value = 50;
                        if (valImgRadius) valImgRadius.innerText = "50%";
                    }
                }
            }
        });
    }

    // Chuyển ảnh Xuống dưới / Lên trên
    if (btnSendBack) {
        btnSendBack.addEventListener("click", function () {
            if (activeElement) activeElement.style.zIndex = "1";
        });
    }

    if (btnBringFront) {
        btnBringFront.addEventListener("click", function () {
            if (activeElement) activeElement.style.zIndex = "10";
        });
    }

    // Slider chỉnh kích thước ảnh
    if (sliderImgWidth) {
        sliderImgWidth.addEventListener("input", function () {
            if (activeElement) {
                const img = activeElement.querySelector("img");
                if (img) {
                    img.style.width = this.value + "px";
                    if (img.style.borderRadius === "50%") {
                        img.style.height = this.value + "px";
                    }
                    if (valImgWidth) valImgWidth.innerText = this.value + "px";
                }
            }
        });
    }

    // Slider chỉnh bo góc ảnh (%)
    if (sliderImgRadius) {
        sliderImgRadius.addEventListener("input", function () {
            if (activeElement) {
                const img = activeElement.querySelector("img");
                if (img) {
                    img.style.borderRadius = this.value + "%";
                    if (parseInt(this.value) === 50) {
                        img.style.height = img.style.width;
                    }
                    if (valImgRadius) valImgRadius.innerText = this.value + "%";
                }
            }
        });
    }

    // ==========================================
    // 8. ĐỊNH DẠNG VĂN BẢN (FONT, COLOR, BOLD, ITALIC)
    // ==========================================
    if (selectFont) {
        selectFont.addEventListener("change", function () {
            if (activeElement) activeElement.style.fontFamily = this.value;
        });
    }

    if (selectFontSize) {
        selectFontSize.addEventListener("change", function () {
            if (activeElement) activeElement.style.fontSize = this.value;
        });
    }

    if (inputColor) {
        inputColor.addEventListener("input", function () {
            if (activeElement) activeElement.style.color = this.value;
        });
    }

    if (btnBold) {
        btnBold.addEventListener("click", function () {
            if (activeElement) {
                const isCurrentlyBold = activeElement.style.fontWeight === "bold" || activeElement.style.fontWeight === "700";
                activeElement.style.fontWeight = isCurrentlyBold ? "normal" : "bold";
                this.classList.toggle("btn-primary", !isCurrentlyBold);
                this.classList.toggle("btn-outline-secondary", isCurrentlyBold);
            }
        });
    }

    if (btnItalic) {
        btnItalic.addEventListener("click", function () {
            if (activeElement) {
                const isCurrentlyItalic = activeElement.style.fontStyle === "italic";
                activeElement.style.fontStyle = isCurrentlyItalic ? "normal" : "italic";
                this.classList.toggle("btn-primary", !isCurrentlyItalic);
                this.classList.toggle("btn-outline-secondary", isCurrentlyItalic);
            }
        });
    }

    // ==========================================
    // 9. THÊM CHỮ MỚI & STICKER
    // ==========================================
    if (btnAddText) {
        btnAddText.addEventListener("click", function () {
            const newDiv = document.createElement("div");
            newDiv.className = "draggable-text-box position-absolute active";
            newDiv.style.cssText = "top: 20px; left: 20px; width: 300px; color: #000000; font-family: Arial; font-size: 18px;";
            newDiv.innerHTML = `<span class="text-content">Lời chúc mới</span>`;

            cardArea.appendChild(newDiv);
            makeDraggable(newDiv);
            newDiv.dispatchEvent(new Event("mousedown"));
        });
    }

    document.querySelectorAll(".sticker-opt").forEach(sticker => {
        sticker.addEventListener("click", function () {
            const emoji = this.textContent;
            const stickerDiv = document.createElement("div");
            stickerDiv.className = "draggable-element position-absolute";
            stickerDiv.style.cssText = "top: 50px; left: 50px; font-size: 35px; cursor: move; user-select: none; z-index: 3;";
            stickerDiv.innerHTML = `<span>${emoji}</span>`;

            cardArea.appendChild(stickerDiv);
            makeDraggable(stickerDiv);
            stickerDiv.dispatchEvent(new Event("mousedown"));
        });
    });

    // ==========================================
    // 10. XÓA VẬT THỂ ĐANG CHỌN
    // ==========================================
    if (btnClearCanvas) {
        btnClearCanvas.addEventListener("click", function () {
            if (activeElement && cardArea.children.length > 1) {
                if (activeElement.querySelector("img")) {
                    uploadedImagePath = "";
                }
                activeElement.remove();
                activeElement = null;
                if (imageTools) imageTools.classList.add("d-none");
            }
        });
    }

    // ==========================================
    // 11. PHÁT NHẠC NỀN
    // ==========================================
    if (selectAudio && audioPlayer) {
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
    }

    // ==========================================
    // 12. GỬI VÀ LƯU CẤU HÌNH THIỆP
    // ==========================================
    if (btnSaveCard) {
        btnSaveCard.addEventListener("click", async function () {
            const recipientEmail = (document.getElementById("input-recipient")?.value ||
                document.getElementById("tpl-recipient")?.value || "").trim();

            const subject = (document.getElementById("input-subject")?.value || "Thiệp chúc mừng").trim();

            if (!recipientEmail) {
                alert("Vui lòng nhập Email người nhận!");
                return;
            }

            // Xóa hết khung viền chọn trước khi lấy HTML lưu vào CSDL
            document.querySelectorAll(".draggable-text-box, .draggable-element").forEach(el => {
                el.classList.remove("active");
                el.style.outline = "none";
            });

            const cardHtml = cardArea.innerHTML;
            const imgElement = cardArea.querySelector("img");
            const finalImagePath = uploadedImagePath || (imgElement ? imgElement.src : "");
            const audioPath = selectAudio ? selectAudio.value : "";
            const templateId = document.getElementById("template-id")?.value || 1;

            const payload = {
                templateId: parseInt(templateId),
                recipientEmail: recipientEmail,
                subject: subject,
                imagePath: finalImagePath,
                audioPath: audioPath,
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
                    try {
                        await navigator.clipboard.writeText(fullCardLink);
                    } catch (e) { }
                    alert("🎉 " + result.message + "\n\nLink thiệp đã được copy!");
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
    }
});