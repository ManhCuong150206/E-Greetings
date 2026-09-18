document.addEventListener("DOMContentLoaded", function () {
    // 1. Khởi tạo Canvas
    const canvas = new fabric.Canvas('cardCanvas', {
        width: 650,
        height: 480
    });

    const bgImageUrl = window.cardBgImagePath || "";

    // 2. Khởi tạo các đoạn chữ ban đầu
    const recipientInput = document.getElementById('inputRecipient');
    const messageInput = document.getElementById('inputMessage');
    const senderInput = document.getElementById('inputSender');

    const recipientText = new fabric.IText(recipientInput ? recipientInput.value : '', {
        left: 50,
        top: 60,
        fontFamily: 'Arial',
        fontSize: 24,
        fontWeight: 'bold',
        fill: '#2c3e50',
        cornerStyle: 'circle'
    });

    const messageText = new fabric.IText(messageInput ? messageInput.value : '', {
        left: 50,
        top: 150,
        fontFamily: 'Arial',
        fontSize: 20,
        fill: '#2c3e50',
        width: 550,
        splitByGrapheme: true,
        cornerStyle: 'circle'
    });

    const senderText = new fabric.IText(senderInput ? senderInput.value : '', {
        left: 350,
        top: 380,
        fontFamily: 'Arial',
        fontSize: 22,
        fontStyle: 'italic',
        fill: '#2c3e50',
        cornerStyle: 'circle'
    });

    // 3. Render nền và chữ
    if (bgImageUrl) {
        fabric.Image.fromURL(bgImageUrl, function (img) {
            img.set({
                scaleX: canvas.width / img.width,
                scaleY: canvas.height / img.height,
                selectable: false,
                evented: false
            });
            canvas.setBackgroundImage(img, canvas.renderAll.bind(canvas));

            canvas.add(recipientText);
            canvas.add(messageText);
            canvas.add(senderText);
            canvas.renderAll();
        }, { crossOrigin: 'anonymous' });
    }

    // --- LIVE BINDING NHẬP LIỆU ---
    if (recipientInput) {
        recipientInput.addEventListener('input', function () {
            recipientText.set('text', this.value || ' ');
            canvas.renderAll();
        });
    }

    if (messageInput) {
        messageInput.addEventListener('input', function () {
            messageText.set('text', this.value || ' ');
            canvas.renderAll();
        });
    }

    if (senderInput) {
        senderInput.addEventListener('input', function () {
            senderText.set('text', this.value || ' ');
            canvas.renderAll();
        });
    }

    // --- ĐỊNH DẠNG VĂN BẢN NÂNG CAO ---
    const textColorInput = document.getElementById('textColor');
    if (textColorInput) {
        textColorInput.addEventListener('input', function () {
            const activeObj = canvas.getActiveObject();
            if (activeObj && activeObj.isType('i-text')) {
                activeObj.set('fill', this.value);
            } else {
                canvas.getObjects('i-text').forEach(obj => obj.set('fill', this.value));
            }
            canvas.renderAll();
        });
    }

    const fontFamilySelect = document.getElementById('fontFamily');
    if (fontFamilySelect) {
        fontFamilySelect.addEventListener('change', function () {
            const activeObj = canvas.getActiveObject();
            if (activeObj && activeObj.isType('i-text')) {
                activeObj.set('fontFamily', this.value);
            } else {
                canvas.getObjects('i-text').forEach(obj => obj.set('fontFamily', this.value));
            }
            canvas.renderAll();
        });
    }

    // Cỡ chữ
    const fontSizeInput = document.getElementById('fontSizeInput');
    if (fontSizeInput) {
        fontSizeInput.addEventListener('input', function () {
            const activeObj = canvas.getActiveObject();
            if (activeObj && activeObj.isType('i-text')) {
                activeObj.set('fontSize', parseInt(this.value, 10) || 20);
                canvas.renderAll();
            }
        });
    }

    // In đậm / In nghiêng
    const btnBold = document.getElementById('btnBold');
    if (btnBold) {
        btnBold.addEventListener('click', function () {
            const activeObj = canvas.getActiveObject();
            if (activeObj && activeObj.isType('i-text')) {
                const isBold = activeObj.fontWeight === 'bold';
                activeObj.set('fontWeight', isBold ? 'normal' : 'bold');
                this.classList.toggle('btn-primary', !isBold);
                this.classList.toggle('btn-outline-secondary', isBold);
                canvas.renderAll();
            }
        });
    }

    const btnItalic = document.getElementById('btnItalic');
    if (btnItalic) {
        btnItalic.addEventListener('click', function () {
            const activeObj = canvas.getActiveObject();
            if (activeObj && activeObj.isType('i-text')) {
                const isItalic = activeObj.fontStyle === 'italic';
                activeObj.set('fontStyle', isItalic ? 'normal' : 'italic');
                this.classList.toggle('btn-primary', !isItalic);
                this.classList.toggle('btn-outline-secondary', isItalic);
                canvas.renderAll();
            }
        });
    }

    // --- BỘ STICKER ICON ---
    document.querySelectorAll('.btn-sticker').forEach(btn => {
        btn.addEventListener('click', function () {
            const emoji = this.getAttribute('data-sticker');
            const stickerText = new fabric.Text(emoji, {
                left: canvas.width / 2 - 20,
                top: canvas.height / 2 - 20,
                fontSize: 45,
                cornerStyle: 'circle'
            });
            canvas.add(stickerText);
            canvas.setActiveObject(stickerText);
            canvas.renderAll();
        });
    });

    // --- TÙY CHỈNH ĐỐI TƯỢNG ĐANG CHỌN (XÓA / ĐỘ TRONG SUỐT) ---
    const opacitySlider = document.getElementById('opacitySlider');
    if (opacitySlider) {
        opacitySlider.addEventListener('input', function () {
            const activeObj = canvas.getActiveObject();
            if (activeObj) {
                activeObj.set('opacity', parseFloat(this.value));
                canvas.renderAll();
            }
        });
    }

    const btnDeleteObject = document.getElementById('btnDeleteObject');
    if (btnDeleteObject) {
        btnDeleteObject.addEventListener('click', function () {
            const activeObj = canvas.getActiveObject();
            if (activeObj) {
                canvas.remove(activeObj);
                canvas.discardActiveObject();
                canvas.renderAll();
            }
        });
    }

    // Lắng nghe sự kiện click đối tượng để đồng bộ thanh công cụ
    canvas.on('selection:created', updateSelectionUI);
    canvas.on('selection:updated', updateSelectionUI);

    function updateSelectionUI(e) {
        const activeObj = e.selected[0];
        if (activeObj) {
            if (opacitySlider) opacitySlider.value = activeObj.opacity || 1;
            if (activeObj.isType('i-text') && fontSizeInput) {
                fontSizeInput.value = activeObj.fontSize;
            }
        }
    }

    // --- UPLOAD ẢNH CÁ NHÂN & XUẤT PNG ---
    const imageUploader = document.getElementById('imageUploader');
    if (imageUploader) {
        imageUploader.addEventListener('change', function (e) {
            const file = e.target.files[0];
            if (!file) return;

            const reader = new FileReader();
            reader.onload = function (f) {
                fabric.Image.fromURL(f.target.result, function (img) {
                    img.scaleToWidth(130);
                    img.set({
                        left: 260,
                        top: 260,
                        cornerStyle: 'circle'
                    });
                    canvas.add(img);
                    canvas.setActiveObject(img);
                });
            };
            reader.readAsDataURL(file);
        });
    }

    const btnExport = document.getElementById('btnExport');
    if (btnExport) {
        btnExport.addEventListener('click', function () {
            const dataURL = canvas.toDataURL({ format: 'png', quality: 1 });
            const link = document.createElement('a');
            link.download = 'Thiệp_EGreeting.png';
            link.href = dataURL;
            link.click();
        });
    }

    // --- NHẠC NỀN MP3 ---
    const bgAudio = document.getElementById('bgAudio');
    const musicUploader = document.getElementById('musicUploader');
    const btnToggleMusic = document.getElementById('btnToggleMusic');
    const musicStatus = document.getElementById('musicStatus');

    if (musicUploader && bgAudio && btnToggleMusic && musicStatus) {
        musicUploader.addEventListener('change', function (e) {
            const file = e.target.files[0];
            if (!file) return;

            const audioUrl = URL.createObjectURL(file);
            bgAudio.src = audioUrl;

            bgAudio.play().then(() => {
                btnToggleMusic.classList.remove('d-none');
                musicStatus.innerText = "Tắt Nhạc";
                btnToggleMusic.className = "btn btn-outline-primary btn-sm rounded-pill py-0 px-2";
            }).catch(() => {
                btnToggleMusic.classList.remove('d-none');
                musicStatus.innerText = "Bật Nhạc";
                btnToggleMusic.className = "btn btn-outline-secondary btn-sm rounded-pill py-0 px-2";
            });
        });

        btnToggleMusic.addEventListener('click', function () {
            if (!bgAudio.paused) {
                bgAudio.pause();
                musicStatus.innerText = "Bật Nhạc";
                this.className = "btn btn-outline-secondary btn-sm rounded-pill py-0 px-2";
            } else {
                bgAudio.play().then(() => {
                    musicStatus.innerText = "Tắt Nhạc";
                    this.className = "btn btn-outline-primary btn-sm rounded-pill py-0 px-2";
                });
            }
        });
    }
});