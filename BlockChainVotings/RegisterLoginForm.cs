﻿using MaterialSkin;
using MaterialSkin.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Virgil.Crypto;

namespace BlockChainVotings
{
    public partial class RegisterLoginForm : MaterialForm
    {

        public event EventHandler SuccsessLogin;

        public RegisterLoginForm()
        {
            InitializeComponent();

            MaterialSkinManager.Instance.AddFormToManage(this);

            panelFixer.BackColor = MaterialSkinManager.Instance.ColorScheme.PrimaryColor;

            this.Text = Properties.Resources.loginIntoNet;
            labelPassLogin.Text = Properties.Resources.pass;
            labelPasswordRegister.Text = Properties.Resources.pass;
            labelPasswordRegister2.Text = Properties.Resources.passRepeat;
            labelPrivateKeyRegister.Text = Properties.Resources.privateKey;
            labelPublicKeyLogin.Text = Properties.Resources.userHash;
            labelPublicKeyRegister.Text = Properties.Resources.userHash;
            buttonLogin.Text = Properties.Resources.toLogin;
            buttonRegister.Text = Properties.Resources.register;
            tabPageLogin.Text = Properties.Resources.login;
            tabPageRegister.Text = Properties.Resources.registration;



            if (VotingsUser.CheckUserExists())
            {
                VotingsUser.GetKeysFromConfig();

                tabControl1.SelectTab("tabPageLogin");
                textBoxPublicKeyLogin.Text = VotingsUser.PublicKey;

                CommonHelpers.ChangeTheme(VotingsUser.Theme);

            }
            else
            {
                tabControl1.SelectTab("tabPageRegister");
                var tab = tabControl1.TabPages["tabPageLogin"];
                tabControl1.TabPages.Remove(tab);
            }


            Icon = Properties.Resources.votingIcon;


        }

        private void textBoxPublicKeyRegister_TextChanged(object sender, EventArgs e)
        {
            if (CommonHelpers.CheckKeys(textBoxPublicKeyRegister.Text, textBoxPrivateKeyRegister.Text))
            {
                textBoxPublicKeyRegister.BackColor = Color.Honeydew;
                textBoxPrivateKeyRegister.BackColor = Color.Honeydew;
            }
            else
            {
                textBoxPublicKeyRegister.BackColor = Color.MistyRose;
                textBoxPrivateKeyRegister.BackColor = Color.MistyRose;
            }
        }

        private void textBoxPasswordRegister_TextChanged(object sender, EventArgs e)
        {
            if (textBoxPasswordRegister.Text == textBoxPasswordRegister2.Text
                && textBoxPasswordRegister.Text.Length >= 3 && textBoxPasswordRegister.Text.Length <= 20)
            {
                textBoxPasswordRegister.BackColor = Color.Honeydew;
                textBoxPasswordRegister2.BackColor = Color.Honeydew;
            }
            else
            {
                textBoxPasswordRegister.BackColor = Color.MistyRose;
                textBoxPasswordRegister2.BackColor = Color.MistyRose;
            }
        }

        private void buttonRegister_Click(object sender, EventArgs e)
        {
            if (textBoxPublicKeyRegister.BackColor == Color.Honeydew &&
                textBoxPrivateKeyRegister.BackColor == Color.Honeydew &&
                textBoxPasswordRegister.BackColor == Color.Honeydew)
            {
                VotingsUser.ClearUserData();
                VotingsUser.Register(textBoxPublicKeyRegister.Text, textBoxPrivateKeyRegister.Text, textBoxPasswordRegister.Text);
                VotingsUser.GetKeysFromConfig();

                if (VotingsUser.Login(textBoxPasswordRegister.Text))
                {
                    SuccsessLogin?.Invoke(this, new EventArgs());
                    Hide();
                }
            }
        }

        private void textBoxPassLogin_TextChanged(object sender, EventArgs e)
        {
            if (CommonHelpers.CalcHash(textBoxPassLogin.Text) == VotingsUser.PasswordHash)
            {
                textBoxPassLogin.BackColor = Color.Honeydew;
            }
            else
            {
                textBoxPassLogin.BackColor = Color.MistyRose;
            }
        }

        private void buttonLogin_Click(object sender, EventArgs e)
        {
            if (textBoxPassLogin.BackColor == Color.Honeydew)
            {
                if (VotingsUser.Login(textBoxPassLogin.Text))
                {
                    SuccsessLogin?.Invoke(this, new EventArgs());
                    Hide();
                }
            }
        }

        private void RegisterLoginForm_Shown(object sender, EventArgs e)
        {
            Task.Run(() => CommonHelpers.GetTime());
            Task.Run(() =>
            {
                try
                {
                    if (!System.IO.File.Exists("Community.CsharpSqlite.dll") ||
                    !System.IO.File.Exists("Community.CsharpSqlite.SQLiteClient.dll") ||
                    !System.IO.File.Exists("NetworkCommsDotNetComplete.dll") ||
                    !System.IO.File.Exists("Virgil.Crypto.dll") ||
                    !System.IO.File.Exists("MaterialSkin.dll"))
                        throw new Exception();

                    CryptoHelper.GenerateKeyPair();
                }
                catch
                {
                    this.Invoke(new Action(() =>
                    {
                        MessageBox.Show(Properties.Resources.libsError, "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Close();
                    }
                    ));
                }
            });
        }
    }
}
