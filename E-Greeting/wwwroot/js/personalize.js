document.addEventListener("DOMContentLoaded", function () {
    // --- 1. DECLARE VARIABLES & GET DOM ELEMENTS ---
    const cardArea = document.getElementById("card-preview-area");
    const selectFont = document.getElementById("select-font");
    const selectFontSize = document.getElementById("select-fontsize");
    const inputColor = document.getElementById("input-color");
    const btnBold = document.getElementById("btn-bold");
    const btnItalic = document.getElementById("btn-italic");
    const selectAudio = document.getElementById("select-audio");
    const audioPlayer = document.getElementById("audio-player");

    const btnAddText = document.getElementById("btn-add-text");
    const btnClearCanvas = document.getElementById("btn-clear-canvas");
    const inputActiveTextVal = document.getElementById("input-active-text-val");

    const sliderTextWidth = document.getElementById("slider-text-width");
    const valTextWidth = document.getElementById("val-text-width");

    const inputUploadImage = document.getElementById("input-upload-image");
    const imageTools = document.getElementById("image-tools");
    const sliderImgWidth = document.getElementById("slider-img-width");
    const sliderImgRadius = document.getElementById("slider-img-radius");
    const valImgWidth = document.getElementById("val-img-width");
    const valImgRadius = document.getElementById("val-img-radius");
    const btnImgCircle = document.getElementById("btn-img-circle");
    const btnSendBack = document.getElementById("btn-send-back");
    const btnBringFront = document.getElementById("btn-bring-front");

    let activeElement = document.querySelector(".draggable-text-box.active, .draggable-element.active") ||
        document.querySelector(".draggable-text-box, .draggable-element");

    // --- 2. HELPER FUNCTION: CONVERT RGB TO HEX ---
    function rgbToHex(rgbStr) {
        if (!rgbStr || rgbStr.startsWith('#')) return rgbStr || "#000000";
        const matches = rgbStr.match(/\d+/g);
        if (!matches || matches.length < 3) return '#000000';
        const r = parseInt(matches[0]).toString(16).padStart(2, '0');
        const g = parseInt(matches[1]).toString(16).padStart(2, '0');
        const b = parseInt(matches[2]).toString(16).padStart(2, '0');
        return `#${r}${g}${b}`;
    }

    // --- 3. SYNC TOOLBAR CONTROLS WITH SELECTED ELEMENT ---
    function syncControlsWithElement(element) {
        if (!element) return;

        const imgTag = element.querySelector("img");

        if (imgTag) {
            if (imageTools) imageTools.classList.remove("d-none");

            const currentWidth = parseInt(imgTag.style.width) || imgTag.offsetWidth || 120;
            const currentRadius = parseInt(imgTag.style.borderRadius) || 0;

            if (sliderImgWidth) sliderImgWidth.value = currentWidth;
            if (valImgWidth) valImgWidth.innerText = currentWidth + "px";

            if (sliderImgRadius) sliderImgRadius.value = currentRadius;
            if (valImgRadius) valImgRadius.innerText = currentRadius + "%";
        } else {
            if (imageTools) imageTools.classList.add("d-none");

            const textSpan = element.querySelector(".text-content") || element;
            if (textSpan && inputActiveTextVal) {
                inputActiveTextVal.value = textSpan.innerText || textSpan.textContent || "";
            }

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

    // --- 4. REAL-TIME TEXT EDITING ---
    if (inputActiveTextVal) {
        inputActiveTextVal.addEventListener("input", function () {
            if (activeElement && !activeElement.querySelector("img")) {
                const textSpan = activeElement.querySelector(".text-content");
                if (textSpan) {
                    textSpan.innerText = this.value || " ";
                } else {
                    activeElement.innerText = this.value || " ";
                }
            }
        });
    }

    // --- 5. DRAG AND DROP FUNCTIONALITY ---
    function makeDraggable(element) {
        let isDragging = false;
        let offsetX = 0, offsetY = 0;

        function onStart(e) {
            isDragging = true;
            activeElement = element;

            document.querySelectorAll(".draggable-text-box, .draggable-element").forEach(el => {
                el.classList.remove("active");
                el.style.border = "none";
            });

            element.classList.add("active");
            element.style.border = "2px dashed #0d6efd";
            syncControlsWithElement(element);

            const clientX = e.touches ? e.touches[0].clientX : e.clientX;
            const clientY = e.touches ? e.touches[0].clientY : e.clientY;

            const rect = element.getBoundingClientRect();
            offsetX = clientX - rect.left;
            offsetY = clientY - rect.top;

            document.addEventListener("mousemove", onMove);
            document.addEventListener("mouseup", onEnd);
            document.addEventListener("touchmove", onMove, { passive: false });
            document.addEventListener("touchend", onEnd);
        }

        function onMove(e) {
            if (!isDragging || activeElement !== element) return;
            if (e.cancelable) e.preventDefault();

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
            document.removeEventListener("mousemove", onMove);
            document.removeEventListener("mouseup", onEnd);
            document.removeEventListener("touchmove", onMove);
            document.removeEventListener("touchend", onEnd);
        }

        element.addEventListener("mousedown", onStart);
        element.addEventListener("touchstart", onStart, { passive: false });
    }

    if (cardArea) {
        cardArea.querySelectorAll("*").forEach(el => {
            if (el.parentElement === cardArea) {
                el.classList.add("draggable-element");
                makeDraggable(el);
            }
        });
    }

    if (activeElement) syncControlsWithElement(activeElement);

    // --- 6. UPLOAD PERSONAL IMAGE & AUTOMATIC COMPRESSION ---
    if (inputUploadImage) {
        inputUploadImage.addEventListener("change", function (e) {
            const file = e.target.files[0];
            if (!file) return;

            const reader = new FileReader();
            reader.onload = function (event) {
                const tempImg = new Image();
                tempImg.onload = function () {
                    const canvas = document.createElement("canvas");
                    const maxDim = 300;
                    let w = tempImg.width;
                    let h = tempImg.height;

                    if (w > h) {
                        if (w > maxDim) { h *= maxDim / w; w = maxDim; }
                    } else {
                        if (h > maxDim) { w *= maxDim / h; h = maxDim; }
                    }

                    canvas.width = w;
                    canvas.height = h;
                    const ctx = canvas.getContext("2d");
                    ctx.drawImage(tempImg, 0, 0, w, h);

                    const compressedBase64 = canvas.toDataURL("image/jpeg", 0.7);

                    const imgContainer = document.createElement("div");
                    imgContainer.className = "draggable-element position-absolute d-inline-block active";
                    imgContainer.style.cssText = "top: 40px; left: 40px; cursor: move; user-select: none; z-index: 15;";

                    const img = document.createElement("img");
                    img.src = compressedBase64;
                    img.style.cssText = "width: 120px; height: 120px; object-fit: cover; border-radius: 0%; display: block; pointer-events: none;";

                    imgContainer.appendChild(img);
                    cardArea.appendChild(imgContainer);

                    makeDraggable(imgContainer);
                    activeElement = imgContainer;
                    syncControlsWithElement(imgContainer);
                    inputUploadImage.value = "";
                };
                tempImg.src = event.target.result;
            };
            reader.readAsDataURL(file);
        });
    }

    // --- 7. IMAGE & TEXT ADJUSTMENT CONTROLS ---
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
                        img.style.height = (img.offsetWidth || parseInt(img.style.width) || 120) + "px";
                        if (sliderImgRadius) sliderImgRadius.value = 50;
                        if (valImgRadius) valImgRadius.innerText = "50%";
                    }
                }
            }
        });
    }

    if (btnSendBack) {
        btnSendBack.addEventListener("click", function () {
            if (activeElement) activeElement.style.zIndex = "1";
        });
    }

    if (btnBringFront) {
        btnBringFront.addEventListener("click", function () {
            if (activeElement) activeElement.style.zIndex = "20";
        });
    }

    if (sliderImgWidth) {
        sliderImgWidth.addEventListener("input", function () {
            if (activeElement) {
                const img = activeElement.querySelector("img");
                if (img) {
                    const newW = this.value + "px";
                    img.style.width = newW;
                    if (img.style.borderRadius === "50%") img.style.height = newW;
                    if (valImgWidth) valImgWidth.innerText = newW;
                }
            }
        });
    }

    if (sliderImgRadius) {
        sliderImgRadius.addEventListener("input", function () {
            if (activeElement) {
                const img = activeElement.querySelector("img");
                if (img) {
                    const newR = this.value + "%";
                    img.style.borderRadius = newR;
                    if (parseInt(this.value) === 50) img.style.height = img.style.width;
                    if (valImgRadius) valImgRadius.innerText = newR;
                }
            }
        });
    }

    if (btnAddText) {
        btnAddText.addEventListener("click", function () {
            const newDiv = document.createElement("div");
            newDiv.className = "draggable-text-box position-absolute active p-2 rounded text-center";
            newDiv.style.cssText = "top: 30px; left: 30px; width: 300px; color: #000000; font-family: Arial; font-size: 20px; z-index: 18; cursor: move; border: 2px dashed #0d6efd; background: rgba(255,255,255,0.4);";
            newDiv.innerHTML = `<span class="text-content" style="white-space: pre-wrap; display: block;">New Text</span>`;

            cardArea.appendChild(newDiv);
            makeDraggable(newDiv);

            activeElement = newDiv;
            syncControlsWithElement(newDiv);
        });
    }

    if (btnClearCanvas) {
        btnClearCanvas.addEventListener("click", function () {
            if (activeElement && cardArea.children.length > 0) {
                activeElement.remove();
                activeElement = cardArea.querySelector(".draggable-text-box, .draggable-element");
                if (activeElement) {
                    activeElement.classList.add("active");
                    syncControlsWithElement(activeElement);
                } else {
                    if (inputActiveTextVal) inputActiveTextVal.value = "";
                }
            }
        });
    }

    if (selectFont) {
        selectFont.addEventListener("change", function () {
            if (activeElement && !activeElement.querySelector("img")) activeElement.style.fontFamily = this.value;
        });
    }

    if (selectFontSize) {
        selectFontSize.addEventListener("change", function () {
            if (activeElement && !activeElement.querySelector("img")) activeElement.style.fontSize = this.value;
        });
    }

    if (inputColor) {
        inputColor.addEventListener("input", function () {
            if (activeElement && !activeElement.querySelector("img")) activeElement.style.color = this.value;
        });
    }

    if (btnBold) {
        btnBold.addEventListener("click", function () {
            if (activeElement && !activeElement.querySelector("img")) {
                const isBold = activeElement.style.fontWeight === "bold" || activeElement.style.fontWeight === "700";
                activeElement.style.fontWeight = isBold ? "normal" : "bold";
                this.classList.toggle("btn-primary", !isBold);
                this.classList.toggle("btn-outline-secondary", isBold);
            }
        });
    }

    if (btnItalic) {
        btnItalic.addEventListener("click", function () {
            if (activeElement && !activeElement.querySelector("img")) {
                const isItalic = activeElement.style.fontStyle === "italic";
                activeElement.style.fontStyle = isItalic ? "normal" : "italic";
                this.classList.toggle("btn-primary", !isItalic);
                this.classList.toggle("btn-outline-secondary", isItalic);
            }
        });
    }

    if (sliderTextWidth) {
        sliderTextWidth.addEventListener("input", function () {
            if (valTextWidth) valTextWidth.innerText = this.value + "px";
            if (activeElement && !activeElement.querySelector("img")) activeElement.style.width = this.value + "px";
        });
    }

    // STICKERS
    document.querySelectorAll(".sticker-opt").forEach(sticker => {
        sticker.addEventListener("click", function () {
            const stickerDiv = document.createElement("div");
            stickerDiv.className = "draggable-element position-absolute active";
            stickerDiv.style.cssText = "top: 50px; left: 50px; font-size: 35px; cursor: move; user-select: none; z-index: 19;";
            stickerDiv.innerHTML = `<span>${this.textContent}</span>`;

            cardArea.appendChild(stickerDiv);
            makeDraggable(stickerDiv);
            activeElement = stickerDiv;
            syncControlsWithElement(stickerDiv);
        });
    });

    // BACKGROUND MUSIC
    if (selectAudio && audioPlayer) {
        selectAudio.addEventListener("change", function () {
            if (this.value) {
                audioPlayer.src = this.value;
                audioPlayer.classList.remove("d-none");
                audioPlayer.play().catch(() => { });
            } else {
                audioPlayer.pause();
                audioPlayer.classList.add("d-none");
            }
        });
    }

    // --- LISTEN FOR SCHEDULED DATETIME INPUT CHANGES ---
    document.addEventListener("change", function (e) {
        if (e.target && (e.target.id === "input-scheduled-at" || e.target.id === "scheduledAt" || e.target.type === "datetime-local")) {
            updateSendButtonText();
        }
    });

    document.addEventListener("input", function (e) {
        if (e.target && (e.target.id === "input-scheduled-at" || e.target.id === "scheduledAt" || e.target.type === "datetime-local")) {
            updateSendButtonText();
        }
    });

    updateSendButtonText();
});

// =========================================================================
// 8. FIND SCHEDULED TIME INPUT ELEMENT
// =========================================================================
function getScheduledInput() {
    return document.getElementById("scheduledAt") ||
        document.getElementById("input-scheduled-at") ||
        document.getElementById("scheduledAtInput") ||
        document.querySelector("input[name='ScheduledAt']") ||
        document.querySelector("input[name='scheduledAt']");
}

// =========================================================================
// 9. AUTOMATICALLY UPDATE BUTTON TEXT BASED ON SCHEDULE STATE
// =========================================================================
function updateSendButtonText() {
    const btnSend = document.getElementById("btnFinalSend") || document.getElementById("btnSendCard");
    const scheduledInput = getScheduledInput();

    if (!btnSend) return;

    if (scheduledInput && scheduledInput.value && scheduledInput.value.trim() !== "") {
        btnSend.innerHTML = '<i class="bi bi-clock-history me-1"></i> Confirm Scheduled Delivery';
        btnSend.classList.remove('btn-primary');
        btnSend.classList.add('btn-warning');
    } else {
        btnSend.innerHTML = '<i class="bi bi-check-circle me-1"></i> Confirm & Send';
        btnSend.classList.remove('btn-warning');
        btnSend.classList.add('btn-primary');
    }
}

// =========================================================================
// 10. SAVE AND SEND CARD FUNCTION
// =========================================================================
async function saveAndSendCard() {
    const btnSend = document.getElementById("btnFinalSend") || document.getElementById("btnSendCard");
    const scheduledInput = getScheduledInput();

    // Get scheduled value
    const scheduledValue = (scheduledInput && scheduledInput.value.trim() !== "") ? scheduledInput.value : null;

    console.log("[DEBUG FRONTEND] Scheduled delivery value:", scheduledValue);

    if (btnSend) {
        btnSend.disabled = true;
        btnSend.innerHTML = scheduledValue
            ? '<span class="spinner-border spinner-border-sm me-2"></span>Scheduling...'
            : '<span class="spinner-border spinner-border-sm me-2"></span>Sending...';
    }

    try {
        const templateId = parseInt(
            document.getElementById("template-id")?.value ||
            document.getElementById("templateId")?.value || "0"
        );

        // Collect all recipients from dynamic recipient rows
        const recipientRows = document.querySelectorAll('.recipient-row');
        const recipients = [];

        recipientRows.forEach(row => {
            const name = row.querySelector('.recipient-name')?.value.trim() || "";
            const email = row.querySelector('.recipient-email')?.value.trim() || "";
            if (email) {
                recipients.push({ name: name, email: email });
            }
        });

        // Fallback for single recipient forms
        if (recipients.length === 0) {
            const singleEmail = (
                document.getElementById("input-recipient-email")?.value ||
                document.getElementById("recipientEmail")?.value || ""
            ).trim();
            const singleName = (
                document.getElementById("input-recipient-name")?.value ||
                document.getElementById("recipientName")?.value || ""
            ).trim();

            if (singleEmail) {
                recipients.push({ name: singleName, email: singleEmail });
            }
        }

        const subject = (
            document.getElementById("input-email-subject")?.value ||
            document.getElementById("emailSubject")?.value || ""
        ).trim();

        const location = (
            document.getElementById("input-event-address")?.value ||
            document.getElementById("input-event-location")?.value ||
            document.getElementById("input-location")?.value || ""
        ).trim();

        const eventTime = (
            document.getElementById("input-event-time")?.value ||
            document.getElementById("eventTime")?.value || ""
        );

        const audioPath = document.getElementById("select-audio")?.value || "";
        const base64Image = document.getElementById("preview-card-img")?.src || "";
        const cardArea = document.getElementById("card-preview-area");

        // Payload object
        const payload = {
            templateId: templateId,
            recipientEmail: recipients[0]?.email || "",
            recipientName: recipients[0]?.name || "",
            recipients: recipients,
            subject: subject,
            location: location,
            eventTime: eventTime,
            scheduledAt: scheduledValue,
            scheduledAtStr: scheduledValue,
            audioPath: audioPath,
            cardImageBase64: base64Image,
            imagePath: base64Image,
            cardHtmlContent: cardArea ? cardArea.innerHTML : ""
        };

        const response = await fetch('/Card/SaveCardConfig', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(payload)
        });

        const result = await response.json();

        if (result.success) {
            alert(result.message);
            window.location.href = result.redirectUrl || '/History';
        } else {
            alert(result.message || "An error occurred while saving the card!");
            if (btnSend) {
                btnSend.disabled = false;
                updateSendButtonText();
            }
        }
    } catch (error) {
        console.error("Error sending card:", error);
        alert("A server connection error occurred!");
        if (btnSend) {
            btnSend.disabled = false;
            updateSendButtonText();
        }
    }
}

window.getScheduledInput = getScheduledInput;
window.updateSendButtonText = updateSendButtonText;
window.saveAndSendCard = saveAndSendCard;
window.handleSaveAndSend = saveAndSendCard;