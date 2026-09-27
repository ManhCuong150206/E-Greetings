document.addEventListener("DOMContentLoaded", function () {
    const btnSendReply = document.getElementById("btn-send-reply");
    const replyMessage = document.getElementById("reply-message");
    const cardAudio = document.getElementById("card-audio");

    // Tự động bật nhạc khi người dùng tương tác với trang (tránh chính sách block autoplay của Chrome)
    if (cardAudio) {
        document.body.addEventListener("click", function () {
            if (cardAudio.paused) {
                cardAudio.play().catch(() => { });
            }
        }, { once: true });
    }

    // Xử lý sự kiện gửi lời cảm ơn
    btnSendReply.addEventListener("click", async function () {
        const text = replyMessage.value.trim();
        if (!text) {
            alert("Vui lòng nhập nội dung cảm ơn trước khi gửi!");
            return;
        }

        try {
            btnSendReply.disabled = true;
            btnSendReply.innerText = "Đang gửi...";

            const response = await fetch("/Card/ReplyCard", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    cardId: 101,
                    replyMessage: text
                })
            });

            const result = await response.json();
            if (result.success) {
                alert("🎉 " + result.message);
                replyMessage.value = "";
            } else {
                alert("Lỗi: " + result.message);
            }
        } catch (err) {
            alert("Lỗi kết nối máy chủ!");
        } finally {
            btnSendReply.disabled = false;
            btnSendReply.innerText = "Gửi Phản Hồi";
        }
    });
});